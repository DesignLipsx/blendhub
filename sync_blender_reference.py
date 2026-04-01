"""
Sync Blender Addon
Backup, restore, and sync Blender settings across versions.
"""

import bpy
import os
import shutil
import subprocess
import sys
import threading
import time

from pathlib import Path
from bpy.types import Panel, PropertyGroup, Operator, AddonPreferences
from bpy.props import StringProperty, EnumProperty, BoolProperty, PointerProperty, FloatProperty

from . import keymap


TIMER_INTERVAL = 0.1
RESTORE_DELAY = 2.0
DEFERRED_REPORT_INTERVAL = 1.0

IGNORED_FOLDERS = {'__pycache__', '.cache', '.local', '.git', '.svn', '.idea', '.vscode', 'sync_blender'}
IGNORED_EXTENSIONS = {'.pyc', '.pyo', '.log', '.tmp'}

files_to_check = [
    ("bookmarks", "bookmarks.txt"),
    ("platform_support", "platform_support.txt"),
    ("recent_files", "recent-files.txt"),
    ("recent_searches", "recent-searches.txt"),
    ("startup", "startup.blend"),
    ("userpref", "userpref.blend"),
]

size_cache = {}

backup_total_files = 1
backup_files_done = 0
backup_status_message = ""
backup_is_running = False

restore_total_files = 1
restore_files_done = 0
restore_status_message = ""
restore_is_running = False

sync_total_files = 1
sync_files_done = 0
sync_status_message = ""
sync_is_running = False


def get_restore_versions(restore_directory):
    """Get list of available backup versions."""
    sync_folder_path = Path(restore_directory) / "Sync Blender"
    if not sync_folder_path.is_dir():
        return []
    return [(folder.name, folder.name, "") for folder in sorted(sync_folder_path.iterdir()) if folder.is_dir()]


def update_folder_names(self, context):
    """Dynamically create properties for detected Blender version folders."""
    config_path = Path(bpy.utils.user_resource('CONFIG'))
    parent_path = config_path.parent.parent
    current_version_name = config_path.parent.name

    for folder_path in parent_path.iterdir():
        if not folder_path.is_dir():
            continue
        folder_name = folder_path.name
        if folder_name[0].isdigit() and folder_name != current_version_name:
            prop_name = f'folder_{folder_name}'
            if not hasattr(BLENDER_SYNC_PG_versions, prop_name):
                setattr(BLENDER_SYNC_PG_versions, prop_name, BoolProperty(name=folder_name, description=folder_name))


def get_valid_blender_versions():
    """Get list of other installed Blender versions."""
    config_path = Path(bpy.utils.user_resource('CONFIG'))
    parent_path = config_path.parent.parent
    current_version_name = config_path.parent.name
    return [p.name for p in parent_path.iterdir() if p.is_dir() and p.name[0].isdigit() and p.name != current_version_name]


def count_files_in_dir(directory: Path) -> int:
    """Count files in directory, excluding ignored folders and extensions."""
    if not directory.is_dir():
        return 0
    count = 0
    for f in directory.rglob('*'):
        if f.is_file():
            if any(p in IGNORED_FOLDERS for p in f.parts):
                continue
            if f.suffix in IGNORED_EXTENSIONS:
                continue
            count += 1
    return count


def get_folder_size(path_str):
    """Get total size of folder with caching."""
    if path_str in size_cache:
        return size_cache[path_str]
    path = Path(path_str)
    if not path.is_dir():
        return 0
    total_size = sum(f.stat().st_size for f in path.rglob('*') if f.is_file())
    size_cache[path_str] = total_size
    return total_size


def get_selected_items_size():
    """Calculate total size of selected backup items."""
    backup_files = bpy.context.scene.backup_files
    config_path = Path(bpy.utils.user_resource('CONFIG'))
    blender_base_path = config_path.parent.parent
    current_version_name = config_path.parent.name
    version_path = blender_base_path / current_version_name

    folder_mappings = {
        "addons": (backup_files.addons, version_path / "scripts" / "addons"),
        "presets": (backup_files.presets, version_path / "scripts" / "presets"),
        "extensions": (backup_files.extensions, version_path / "extensions")
    }

    file_mappings = {
        "bookmarks.txt": backup_files.bookmarks,
        "platform_support.txt": backup_files.platform_support,
        "recent-files.txt": backup_files.recent_files,
        "recent-searches.txt": backup_files.recent_searches,
        "startup.blend": backup_files.startup,
        "userpref.blend": backup_files.userpref
    }

    total_size = 0
    for filename, enabled in file_mappings.items():
        if enabled:
            file_path = config_path / filename
            if file_path.is_file():
                total_size += file_path.stat().st_size

    for _, (enabled, path_obj) in folder_mappings.items():
        if enabled and path_obj.is_dir():
            total_size += get_folder_size(str(path_obj))

    return total_size / (1024 * 1024)


def update_total_size(self, context):
    """Calculate size in background thread to prevent UI lag."""
    def calculate_size():
        context.window_manager.backup_total_size = get_selected_items_size()
    threading.Thread(target=calculate_size, daemon=True).start()


def get_sync_blender_prefs():
    """Get addon preferences."""
    return bpy.context.preferences.addons[__package__].preferences


def get_item_existence(context, function_type):
    """Check which items exist for the current operation type."""
    exists = {}
    config_path = Path(bpy.utils.user_resource('CONFIG'))
    current_version_path = config_path.parent
    prefs = get_sync_blender_prefs()

    if function_type in ('BACKUP', 'SYNC'):
        base_path = current_version_path
        config_folder = config_path
    elif function_type == 'RESTORE':
        ui_settings = context.scene.blender_sync_settings if hasattr(context.scene, "blender_sync_settings") else None
        restore_version = ui_settings.restore_version if ui_settings else ""
        if not restore_version:
            return {prop: False for prop, _ in files_to_check} | {"addons": False, "presets": False, "extensions": False}
        restore_dir = prefs.restore_directory if prefs else str(Path.home() / "Documents")
        base_path = Path(restore_dir) / "Sync Blender" / restore_version
        config_folder = base_path / "config"
        if not base_path.exists():
            return {prop: False for prop, _ in files_to_check} | {"addons": False, "presets": False, "extensions": False}
    else:
        return {}

    file_path_map = {
        "bookmarks": config_folder / "bookmarks.txt",
        "platform_support": config_folder / "platform_support.txt",
        "recent_files": config_folder / "recent-files.txt",
        "recent_searches": config_folder / "recent-searches.txt",
        "startup": config_folder / "startup.blend",
        "userpref": config_folder / "userpref.blend",
    }
    for prop, path in file_path_map.items():
        exists[prop] = path.is_file()

    folder_path_map = {
        "addons": base_path / "scripts" / "addons" if function_type in ('BACKUP', 'SYNC') else base_path / "addons",
        "presets": base_path / "scripts" / "presets" if function_type in ('BACKUP', 'SYNC') else base_path / "presets",
        "extensions": base_path / "extensions"
    }
    for prop, path in folder_path_map.items():
        exists[prop] = path.is_dir() and any(path.iterdir())

    return exists


def check_and_show_restore_report():
    """Check for restore flag file and show report if present."""
    flag_file = Path(bpy.utils.user_resource('CONFIG')) / "sync_blender_restore.txt"
    if not flag_file.exists():
        return

    restored_items = []

    def deferred_show_report(restored_list, flag_path):
        if restored_list:
            report_message = "Items Restored:\n" + "\n".join(sorted(list(set(restored_list))))
            try:
                bpy.ops.blender_sync.show_report('INVOKE_DEFAULT', report_text=report_message)
            except RuntimeError:
                return TIMER_INTERVAL
        if flag_path.exists():
            flag_path.unlink()
        return None

    try:
        with open(flag_file, "r") as f:
            lines = f.readlines()
            if "[REPORT]\n" in lines:
                report_start_index = lines.index("[REPORT]\n")
                for line in lines[report_start_index + 1:]:
                    if line.startswith('['):
                        break
                    item = line.strip()
                    if item:
                        cleaned_item = item.replace('-', ' ').replace('.blend', '').replace('.txt', '').replace('userpref', 'Preferences').replace('startup', 'Startup file')
                        restored_items.append(cleaned_item.title())
        if restored_items:
            bpy.app.timers.register(lambda: deferred_show_report(restored_items, flag_file), first_interval=DEFERRED_REPORT_INTERVAL)
    except Exception:
        pass


def update_panel_category(prefs):
    """Re-register panel with updated category."""
    try:
        bpy.utils.unregister_class(BLENDER_SYNC_PT_panel)
    except RuntimeError:
        pass
    BLENDER_SYNC_PT_panel.bl_category = prefs.category
    bpy.utils.register_class(BLENDER_SYNC_PT_panel)


def refresh_restore_versions_on_update(self, context):
    """Refresh restore version list when directory changes."""
    context.scene.blender_sync_settings.refresh_restore_versions(context)


class BLENDER_SYNC_PG_versions(PropertyGroup):
    """Dynamic property group for Blender version folders."""
    pass


class BLENDER_SYNC_PG_folders(PropertyGroup):
    """Property group for file/folder selection."""
    addons: BoolProperty(name="Addons", default=True, update=update_total_size)
    extensions: BoolProperty(name="Extensions", default=True, update=update_total_size)
    bookmarks: BoolProperty(name="Bookmarks", default=True, update=update_total_size)
    platform_support: BoolProperty(name="Platform support", default=True, update=update_total_size)
    presets: BoolProperty(name="Presets", default=True, update=update_total_size)
    recent_files: BoolProperty(name="Recent files", default=True, update=update_total_size)
    recent_searches: BoolProperty(name="Recent searches", default=True, update=update_total_size)
    startup: BoolProperty(name="Startup", default=True, update=update_total_size)
    userpref: BoolProperty(name="Preferences", default=True, update=update_total_size)


class BLENDER_SYNC_PG_UI_SETTINGS(PropertyGroup):
    """UI settings property group."""
    
    def refresh_restore_versions(self, context):
        self["restore_version"] = ""

    function_type: EnumProperty(
        name="Function Type",
        description="Select the operation to perform",
        items=[
            ('BACKUP', "Backup", "Backup folders to a designated location"),
            ('RESTORE', "Restore", "Restore folders from a backup location"),
            ('SYNC', "Sync", "Sync folders to another Blender version")
        ],
        default='BACKUP'
    )

    restore_version: EnumProperty(
        name="Restore from",
        description="Select the Blender backup version for restoring settings",
        items=lambda self, context: get_restore_versions(get_sync_blender_prefs().restore_directory)
    )


class BackupThread(threading.Thread):
    """Thread for handling backup operations."""
    
    def __init__(self, operator, context):
        super().__init__()
        self.operator = operator
        self.context = context
        self.daemon = True

    def run(self):
        global backup_total_files, backup_files_done, backup_status_message, backup_is_running

        prefs = get_sync_blender_prefs()
        backup_files = self.context.scene.backup_files
        backup_is_running = True
        backup_status_message = "Preparing backup..."

        base_path = Path(bpy.utils.resource_path('USER'))
        sync_folder = Path(prefs.backup_directory) / "Sync Blender"
        blender_version = bpy.app.version_string.split(" ")[0]
        versioned_dest_path = sync_folder / blender_version

        config_folder = base_path / "config"
        config_dest_folder = versioned_dest_path / "config"
        config_dest_folder.mkdir(parents=True, exist_ok=True)

        total_files_to_copy = len([(prop, file) for prop, file in files_to_check if getattr(backup_files, prop, False)])
        total_files_to_copy += self.count_selected_folder_files(base_path, backup_files)
        total_files_to_copy = max(1, total_files_to_copy)

        backup_total_files = total_files_to_copy
        backup_files_done = 0

        self.copy_selected_config_files(config_folder, config_dest_folder, backup_files)
        self.copy_selected_folder(base_path / "scripts" / "addons", versioned_dest_path / "addons", "Addon", backup_files.addons)
        self.copy_selected_folder(base_path / "scripts" / "presets", versioned_dest_path / "presets", "Preset", backup_files.presets)
        self.copy_selected_folder(base_path / "extensions", versioned_dest_path / "extensions", "Extension", backup_files.extensions)

        backup_status_message = "Backup complete"
        backup_is_running = False

    def copy_selected_config_files(self, src_folder, dst_folder, backup_files):
        """Copy selected configuration files."""
        global backup_files_done, backup_status_message
        for prop, filename in files_to_check:
            if getattr(backup_files, prop, False):
                src = src_folder / filename
                dst = dst_folder / filename
                if src.exists():
                    try:
                        shutil.copy2(src, dst)
                        backup_status_message = f"Copied: {filename}"
                    except Exception as e:
                        backup_status_message = f"Error copying {filename}: {e}"
                else:
                    backup_status_message = f"File not found: {filename}"
                backup_files_done += 1

    def copy_selected_folder(self, src, dst, label, enabled):
        """Copy folder contents recursively."""
        global backup_files_done, backup_status_message
        if not enabled:
            return
        if not src.exists():
            backup_status_message = f"{label}s folder not found"
            return
        try:
            for src_file in src.rglob('*'):
                if not src_file.is_file():
                    continue
                if any(p in IGNORED_FOLDERS for p in src_file.parts):
                    continue
                if src_file.suffix in IGNORED_EXTENSIONS:
                    continue
                rel_path = src_file.relative_to(src)
                dst_file = dst / rel_path
                dst_file.parent.mkdir(parents=True, exist_ok=True)
                try:
                    shutil.copy2(src_file, dst_file)
                    backup_status_message = f"Copied: {label} - {rel_path}"
                except Exception as e:
                    backup_status_message = f"Error copying {label.lower()}: {e}"
                backup_files_done += 1
        except Exception as e:
            backup_status_message = f"Error walking {label.lower()}s: {e}"

    def count_selected_folder_files(self, base_path, backup_files):
        """Count files in selected folders."""
        count = 0
        if backup_files.addons:
            count += count_files_in_dir(base_path / "scripts" / "addons")
        if backup_files.presets:
            count += count_files_in_dir(base_path / "scripts" / "presets")
        if backup_files.extensions:
            count += count_files_in_dir(base_path / "extensions")
        return count


class RestoreThread(threading.Thread):
    """Thread for handling restore operations."""
    
    def __init__(self, operator, context):
        super().__init__()
        self.operator = operator
        self.context = context
        self.daemon = True

    def run(self):
        global restore_total_files, restore_files_done, restore_status_message, restore_is_running

        try:
            prefs = get_sync_blender_prefs()
            restore_files = self.context.scene.restore_files
            ui_settings = self.context.scene.blender_sync_settings

            restore_is_running = True
            restore_status_message = "Preparing restore..."
            restored_items_report = []

            restore_version = ui_settings.restore_version
            restore_dir_path = Path(prefs.restore_directory) / "Sync Blender" / restore_version
            dest_path = Path(bpy.utils.resource_path('USER'))
            restore_dir = Path(prefs.restore_directory) / "Sync Blender"

            current_theme_name = None
            if restore_files.presets:
                current_theme_name = self.get_current_theme_name()

            if not restore_dir_path.exists() or not any(restore_dir_path.iterdir()):
                restore_status_message = "Restore directory is invalid or empty."
                restore_is_running = False
                return

            if not any(getattr(restore_files, attr, False) for attr, _ in files_to_check) and not (restore_files.addons or restore_files.presets or restore_files.extensions):
                restore_status_message = "No items selected for restore."
                restore_is_running = False
                return

            restore_files_done = 0
            restore_total_files = len([(prop, file) for prop, file in files_to_check if getattr(restore_files, prop, False)])

            def count_if_exists(path):
                return count_files_in_dir(path) if path.exists() else 0

            if restore_files.addons:
                restore_total_files += count_if_exists(restore_dir_path / "addons")
            if restore_files.presets:
                restore_total_files += count_if_exists(restore_dir_path / "presets")
            if restore_files.extensions:
                restore_total_files += count_if_exists(restore_dir_path / "extensions")

            restore_total_files = max(1, restore_total_files)

            config_src = restore_dir_path / "config"
            config_dest = dest_path / "config"
            config_dest.mkdir(exist_ok=True)

            for key, filename in files_to_check:
                if getattr(restore_files, key, False):
                    src_file = config_src / filename
                    dst_file = config_dest / filename
                    if src_file.exists():
                        try:
                            shutil.copy2(src_file, dst_file)
                            restore_status_message = f"Restored: {filename}"
                            restored_items_report.append(filename)
                        except Exception as e:
                            restore_status_message = f"Error restoring {filename}: {e}"
                    else:
                        restore_status_message = f"Missing file: {filename}"
                    restore_files_done += 1

            version_dir = restore_dir / restore_version

            def restore_folder(src_root, dst_root, label):
                global restore_files_done, restore_status_message
                if not src_root.exists():
                    return
                restored_items_report.append(f"{label}s")
                for f in src_root.rglob('*'):
                    if not f.is_file():
                        continue
                    rel_path = f.relative_to(src_root)
                    if any(p in IGNORED_FOLDERS for p in f.parts):
                        continue
                    if f.suffix in IGNORED_EXTENSIONS:
                        continue
                    src = f
                    dst = dst_root / rel_path
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    try:
                        shutil.copy2(src, dst)
                        restore_status_message = f"Restored: {label} - {rel_path.name}"
                    except Exception as e:
                        restore_status_message = f"Error restoring {label}: {e}"
                    restore_files_done += 1

            if restore_files.addons:
                restore_folder(version_dir / "addons", dest_path / "scripts" / "addons", "Addon")
            if restore_files.presets:
                restore_folder(version_dir / "presets", dest_path / "scripts" / "presets", "Preset")
            if restore_files.extensions:
                restore_folder(version_dir / "extensions", dest_path / "extensions", "Extension")

            restore_status_message = f"Restore complete from Blender {restore_version}"
            restore_is_running = False

            time.sleep(RESTORE_DELAY)

            modules_to_enable = []
            flag_file = Path(bpy.utils.user_resource('CONFIG')) / "sync_blender_restore.txt"

            try:
                with open(flag_file, "w") as f:
                    f.write("[REPORT]\n")
                    for item in sorted(list(set(restored_items_report))):
                        f.write(item + "\n")
                    f.write("[MODULES]\n")

                    if restore_files.addons:
                        addons_dir = version_dir / "addons"
                        if addons_dir.exists():
                            addon_modules = [d.name for d in addons_dir.iterdir() if d.is_dir() and d.name != "sync_blender"]
                            modules_to_enable += addon_modules

                    if restore_files.extensions:
                        extensions_dir = version_dir / "extensions"
                        if extensions_dir.exists():
                            extension_modules = [d.name for d in extensions_dir.iterdir() if d.is_dir()]
                            modules_to_enable += extension_modules

                    for module in modules_to_enable:
                        path_parts = Path(module).parts
                        if any(folder in IGNORED_FOLDERS for folder in path_parts):
                            continue
                        if Path(module).suffix in IGNORED_EXTENSIONS:
                            continue
                        f.write(module + "\n")

                    if restore_files.presets and current_theme_name:
                        f.write("[THEME]\n")
                        f.write(current_theme_name + "\n")
            except Exception as e:
                restore_status_message = f"Failed to write addon restore file: {e}"

            blender_exe = bpy.app.binary_path
            current_blend_file = bpy.data.filepath

            try:
                if current_blend_file:
                    if sys.platform.startswith('win'):
                        subprocess.Popen([blender_exe, current_blend_file], creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
                    else:
                        subprocess.Popen([blender_exe, current_blend_file], close_fds=True)
                else:
                    if sys.platform.startswith('win'):
                        subprocess.Popen([blender_exe], creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
                    else:
                        subprocess.Popen([blender_exe], close_fds=True)
            except Exception as e:
                restore_status_message = f"Failed to restart Blender: {e}"

        except Exception as e:
            restore_status_message = f"Critical error during restore: {e}"
            restore_is_running = False

    def get_current_theme_name(self):
        """Get the name of the currently active theme."""
        try:
            active_theme = bpy.context.preferences.themes[0]
            if active_theme.filepath:
                return bpy.path.display_name(Path(active_theme.filepath).name)
            else:
                return "Blender Dark"
        except Exception:
            return None


class SyncThread(threading.Thread):
    """Thread for handling sync operations."""
    
    def __init__(self, operator, context):
        super().__init__()
        self.operator = operator
        self.context = context
        self.daemon = True

    def run(self):
        global sync_total_files, sync_files_done, sync_status_message, sync_is_running

        sync_files = bpy.context.scene.sync_files
        blender_versions = bpy.context.scene.blender_versions
        source_path = Path(bpy.utils.resource_path('USER'))

        sync_is_running = True
        sync_status_message = "Preparing sync..."

        version_flags = [(prop.identifier, getattr(blender_versions, prop.identifier)) for prop in blender_versions.bl_rna.properties if prop.identifier not in {"rna_type", "name"}]

        enabled_versions_count = sum(1 for _, enabled in version_flags if enabled)
        if enabled_versions_count == 0:
            sync_status_message = "No target versions selected."
            sync_is_running = False
            return

        base_total_files = len([(prop, file) for prop, file in files_to_check if getattr(sync_files, prop, False)])
        base_total_files += self.count_selected_folder_files(source_path, sync_files)

        sync_total_files = max(1, base_total_files * enabled_versions_count)
        sync_files_done = 0

        source_version = '.'.join(map(str, bpy.app.version[:2]))
        source_scripts = source_path / "scripts"
        source_addons = source_scripts / "addons"
        source_presets = source_scripts / "presets"
        source_extensions = source_path / "extensions"

        def sync_folder(src_root, dst_root, label):
            global sync_files_done, sync_status_message
            if not src_root.exists():
                return
            for f in src_root.rglob('*'):
                if not f.is_file():
                    continue
                if any(p in IGNORED_FOLDERS for p in f.parts):
                    continue
                if f.suffix in IGNORED_EXTENSIONS:
                    continue
                rel_path = f.relative_to(src_root)
                dst = dst_root / rel_path
                dst.parent.mkdir(parents=True, exist_ok=True)
                try:
                    shutil.copy2(f, dst)
                    sync_status_message = f"Synced {label}: {rel_path}"
                except Exception as e:
                    sync_status_message = f"Error syncing {label}: {e}"
                sync_files_done += 1

        for version_id, enabled in version_flags:
            if not enabled:
                continue

            target_version = version_id[7:].replace("_", ".")
            resource_path_str = str(bpy.utils.resource_path('USER'))
            target_dir = Path(resource_path_str.replace(source_version, target_version))
            target_scripts = target_dir / "scripts"

            config_src = source_path / "config"
            config_dest = target_dir / "config"
            config_dest.mkdir(exist_ok=True)

            for key, filename in files_to_check:
                if getattr(sync_files, key, False):
                    src_file = config_src / filename
                    dst_file = config_dest / filename
                    if src_file.exists():
                        try:
                            shutil.copy2(src_file, dst_file)
                            sync_status_message = f"Synced: {filename} to {target_version}"
                        except Exception as e:
                            sync_status_message = f"Error syncing {filename} to {target_version}: {e}"
                    else:
                        sync_status_message = f"Missing file: {filename}"
                    sync_files_done += 1

            if sync_files.addons:
                sync_folder(source_addons, target_scripts / "addons", "Addon")
            if sync_files.presets:
                sync_folder(source_presets, target_scripts / "presets", "Preset")
            if sync_files.extensions:
                sync_folder(source_extensions, target_dir / "extensions", "Extension")

        sync_status_message = "Sync complete."
        sync_is_running = False

    def count_selected_folder_files(self, base_path, sync_files):
        """Count files in selected folders."""
        count = 0
        if sync_files.addons:
            count += count_files_in_dir(base_path / "scripts" / "addons")
        if sync_files.presets:
            count += count_files_in_dir(base_path / "scripts" / "presets")
        if sync_files.extensions:
            count += count_files_in_dir(base_path / "extensions")
        return count


class BLENDER_REPORT_OT_show_report(Operator):
    """Show restore report dialog"""
    bl_idname = "blender_sync.show_report"
    bl_label = "Blender Sync Restore Report"
    bl_options = {'REGISTER', 'INTERNAL'}

    report_text: StringProperty(name="Report Text")

    def execute(self, context):
        return {'FINISHED'}

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self, width=350)

    def draw(self, context):
        layout = self.layout
        box = layout.box()
        box.label(text="Restore Complete!", icon='CHECKMARK')
        box.separator()
        for line in self.report_text.split('\n'):
            box.label(text=line)


class BLENDER_BACKUP_OT_backup_files(Operator):
    """Backup Blender settings and addons"""
    bl_idname = "blender_sync.backup_files"
    bl_label = "Backup Files"
    bl_options = {'REGISTER', 'UNDO'}

    _timer = None

    def modal(self, context, event):
        global backup_is_running
        prefs = get_sync_blender_prefs()

        if event.type == 'ESC':
            self.report({'WARNING'}, "Operation cancelled by user")
            context.window_manager.event_timer_remove(self._timer)
            backup_is_running = False
            return {'CANCELLED'}

        if event.type == 'TIMER':
            if not backup_is_running:
                self.report({'INFO'}, backup_status_message)
                context.window_manager.event_timer_remove(self._timer)
                return {'FINISHED'}

            progress = backup_files_done / backup_total_files if backup_total_files else 0.0

            if prefs.progress_display_mode == 'PROGRESS_BAR':
                for area in context.window.screen.areas:
                    area.tag_redraw()
            else:
                if backup_is_running:
                    self.report({'INFO'}, f"Backing up - {int(progress * 100)}%")

        return {'PASS_THROUGH'}

    def invoke(self, context, event):
        global backup_is_running

        if backup_is_running:
            self.report({'WARNING'}, "Backup is already running")
            return {'CANCELLED'}

        thread = BackupThread(self, context)
        thread.start()

        wm = context.window_manager
        self._timer = wm.event_timer_add(TIMER_INTERVAL, window=context.window)
        wm.modal_handler_add(self)
        return {'RUNNING_MODAL'}


class BLENDER_RESTORE_OT_restore_files(Operator):
    """Restore Blender settings and addons from backup"""
    bl_idname = "blender_sync.restore_files"
    bl_label = "Restore Files"
    bl_options = {'REGISTER', 'UNDO'}

    _timer = None

    def modal(self, context, event):
        global restore_is_running
        prefs = get_sync_blender_prefs()

        if event.type == 'ESC':
            self.report({'WARNING'}, "Operation cancelled by user")
            context.window_manager.event_timer_remove(self._timer)
            restore_is_running = False
            return {'CANCELLED'}

        if event.type == 'TIMER':
            if not restore_is_running:
                self.report({'INFO'}, restore_status_message)
                context.window_manager.event_timer_remove(self._timer)
                return {'FINISHED'}

            progress = restore_files_done / restore_total_files if restore_total_files else 0.0

            if prefs and prefs.progress_display_mode == 'PROGRESS_BAR':
                for area in context.window.screen.areas:
                    area.tag_redraw()
            else:
                if restore_is_running:
                    self.report({'INFO'}, f"Restoring - {int(progress * 100)}%")

        return {'PASS_THROUGH'}

    def invoke(self, context, event):
        global restore_is_running

        if restore_is_running:
            self.report({'WARNING'}, "A restore process is already running")
            return {'CANCELLED'}

        thread = RestoreThread(self, context)
        thread.start()

        wm = context.window_manager
        self._timer = wm.event_timer_add(TIMER_INTERVAL, window=context.window)
        wm.modal_handler_add(self)

        return {'RUNNING_MODAL'}


class BLENDER_SYNC_OT_sync(Operator):
    """Sync Blender settings and addons to other versions"""
    bl_idname = "blender_sync.sync_files"
    bl_label = "Sync Files"
    bl_options = {'REGISTER', 'UNDO'}

    _timer = None

    def modal(self, context, event):
        global sync_is_running
        prefs = get_sync_blender_prefs()

        if event.type == 'ESC':
            self.report({'WARNING'}, "Operation cancelled by user")
            context.window_manager.event_timer_remove(self._timer)
            sync_is_running = False
            return {'CANCELLED'}

        if event.type == 'TIMER':
            if not sync_is_running:
                self.report({'INFO'}, sync_status_message)
                context.window_manager.event_timer_remove(self._timer)
                return {'FINISHED'}

            progress = sync_files_done / sync_total_files if sync_total_files else 0.0

            if prefs and prefs.progress_display_mode == 'PROGRESS_BAR':
                for area in context.window.screen.areas:
                    area.tag_redraw()
            else:
                if sync_is_running:
                    self.report({'INFO'}, f"Syncing - {int(progress * 100)}%")

        return {'PASS_THROUGH'}

    def invoke(self, context, event):
        global sync_is_running

        if sync_is_running:
            self.report({'WARNING'}, "Sync is already running")
            return {'CANCELLED'}

        thread = SyncThread(self, context)
        thread.start()

        wm = context.window_manager
        self._timer = wm.event_timer_add(TIMER_INTERVAL, window=context.window)
        wm.modal_handler_add(self)
        return {'RUNNING_MODAL'}


def draw_directory_box(box, prefs, is_backup):
    """Draw directory selection box."""
    prop_name = "backup_directory" if is_backup else "restore_directory"
    enable_edit_prop = "enable_edit_backup_directory" if is_backup else "enable_edit_restore_directory"

    dir_path = getattr(prefs, prop_name)
    enable_edit = getattr(prefs, enable_edit_prop)

    if enable_edit:
        box.prop(prefs, prop_name)
    else:
        base_dir_path = Path(dir_path)
        sync_path = base_dir_path / "Sync Blender"
        sync_path.mkdir(parents=True, exist_ok=True)

        row = box.row()
        row.label(text=f"{base_dir_path}{os.sep}Sync Blender")
        row.operator("wm.path_open", text="", icon='FILE_FOLDER', emboss=False).filepath = str(sync_path)


def draw_items_panel(layout, props, items_to_draw):
    """Draw items selection panel."""
    box = layout.box()
    box.label(text="Items", icon='FILE_BACKUP')

    for i in range(0, len(items_to_draw), 2):
        row = box.row()
        prop_name1, label1 = items_to_draw[i]
        row.prop(props, prop_name1, text=label1)

        if i + 1 < len(items_to_draw):
            prop_name2, label2 = items_to_draw[i + 1]
            row.prop(props, prop_name2, text=label2)


def draw_sync_ui(layout, context):
    """Draw main sync UI."""
    scene = context.scene
    prefs = get_sync_blender_prefs()
    data = scene.backup_files, scene.restore_files, scene.sync_files, scene.blender_versions
    backup_files, restore_files, sync_files, blender_versions = data
    wm = context.window_manager
    ui_settings = scene.blender_sync_settings
    function_type = ui_settings.function_type

    layout.prop(ui_settings, "function_type", expand=True)
    layout.separator(factor=0.1)

    folder_properties = [
        ("addons", "Addons"), ("extensions", "Extensions"),
        ("userpref", "Preferences"), ("startup", "Startup file"),
        ("platform_support", "Platform support"), ("presets", "Presets"),
        ("bookmarks", "Bookmarks"), ("recent_files", "Recent files"),
        ("recent_searches", "Recent searches"),
    ]

    exists_map = get_item_existence(context, function_type)
    items_to_draw = [(prop, label) for prop, label in folder_properties if exists_map.get(prop, True)]

    if function_type == 'BACKUP':
        box = layout.box()
        draw_directory_box(box, prefs, is_backup=True)
        draw_items_panel(layout, backup_files, items_to_draw)
        wm.backup_total_size = get_selected_items_size()
        box = layout.box()
        box.label(text=f"Total Backup Size: {wm.backup_total_size:.2f} MB", icon='DISK_DRIVE')

    elif function_type == 'RESTORE':
        box = layout.box()
        draw_directory_box(box, prefs, is_backup=False)
        box = layout.box()
        box.label(text="Restore Version", icon='BLENDER')
        base_restore_directory = Path(prefs.restore_directory)
        restore_path = base_restore_directory / "Sync Blender"
        if not restore_path.exists() or not any(restore_path.iterdir()):
            box.label(text="No Backup Found.", icon='ERROR')
        else:
            box.prop(ui_settings, "restore_version", text="Restore From")
        draw_items_panel(layout, restore_files, items_to_draw)

    elif function_type == 'SYNC':
        box = layout.box()
        blender_version = '.'.join(map(str, bpy.app.version))
        box.label(text=f"Blender Version {blender_version}")
        box = layout.box()
        box.label(text="Sync To", icon='BLENDER')
        valid_versions = get_valid_blender_versions()
        if valid_versions:
            for prop in blender_versions.bl_rna.properties:
                if prop.identifier in {"rna_type", "name"}:
                    continue
                folder_name = prop.description
                box.prop(blender_versions, prop.identifier, text=folder_name)
        else:
            box.label(text="No other Blender versions installed.", icon='INFO')
        draw_items_panel(layout, sync_files, items_to_draw)


class BLENDER_SYNC_PT_panel(Panel):
    """Main Sync Blender panel"""
    bl_label = "Sync Blender"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Sync Blender'

    def draw(self, context):
        layout = self.layout
        draw_sync_ui(self.layout, context)
        ui_settings = context.scene.blender_sync_settings

        row = layout.row()
        row.scale_y = 1.5

        if ui_settings.function_type == 'BACKUP':
            row.operator("blender_sync.backup_files")
        elif ui_settings.function_type == 'RESTORE':
            row.operator("blender_sync.restore_files")
        elif ui_settings.function_type == 'SYNC':
            row.operator("blender_sync.sync_files")


class BLENDER_SYNC_OT_popup(Operator):
    """Show Sync Blender popup dialog"""
    bl_idname = "blender_sync.show_popup"
    bl_label = "Sync Blender"
    bl_options = {'REGISTER', 'INTERNAL'}

    def execute(self, context):
        ui_settings = context.scene.blender_sync_settings

        def deferred_execute():
            if ui_settings.function_type == 'BACKUP':
                bpy.ops.blender_sync.backup_files('INVOKE_DEFAULT')
            elif ui_settings.function_type == 'RESTORE':
                bpy.ops.blender_sync.restore_files('INVOKE_DEFAULT')
            elif ui_settings.function_type == 'SYNC':
                bpy.ops.blender_sync.sync_files('INVOKE_DEFAULT')
            return None

        bpy.app.timers.register(deferred_execute, first_interval=TIMER_INTERVAL)
        return {'FINISHED'}

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self, width=320)

    def draw(self, context):
        draw_sync_ui(self.layout, context)


def draw_menu(self, context):
    """Draw menu item in File menu."""
    layout = self.layout
    layout.separator()
    layout.operator("blender_sync.show_popup", icon='MOD_HUE_SATURATION')


def draw_progress_bar(self, context):
    """Draw progress bar in header."""
    prefs = get_sync_blender_prefs()

    if not prefs or prefs.progress_display_mode != 'PROGRESS_BAR':
        return

    layout = self.layout
    ui_settings = context.scene.blender_sync_settings

    if ui_settings.function_type == 'BACKUP' and backup_is_running:
        progress = backup_files_done / backup_total_files if backup_total_files else 0.0
        row = layout.row()
        row.scale_x = 1.5
        row.progress(factor=progress, text=f"Backing up - {int(progress * 100)}%")

    elif ui_settings.function_type == 'RESTORE' and restore_is_running:
        progress = restore_files_done / restore_total_files if restore_total_files else 0.0
        row = layout.row()
        row.scale_x = 1.5
        row.progress(factor=progress, text=f"Restoring - {int(progress * 100)}%")

    elif ui_settings.function_type == 'SYNC' and sync_is_running:
        progress = sync_files_done / sync_total_files if sync_total_files else 0.0
        row = layout.row()
        row.scale_x = 1.5
        row.progress(factor=progress, text=f"Syncing - {int(progress * 100)}%")


class BLENDER_SYNC_preferences(AddonPreferences):
    """Addon preferences"""
    bl_idname = __package__

    progress_display_mode: EnumProperty(
        name="Progress Display",
        description="Choose how to display live progress",
        items=[
            ('SELF_REPORT', "Status Bar Text", "Display progress as text in the Blender status bar"),
            ('PROGRESS_BAR', "Header Progress Bar", "Display a graphical progress bar in the 3D View header"),
        ],
        default='PROGRESS_BAR'
    )

    category: StringProperty(
        name="Panel Category",
        description="Category to show the Sync Blender panel",
        default="Sync Blender",
        update=lambda self, context: update_panel_category(self),
    )

    backup_directory: StringProperty(
        name="Backup Directory",
        description="Choose the backup location",
        subtype='DIR_PATH',
        default=str(Path.home() / "Documents")
    )

    restore_directory: StringProperty(
        name="Restore Directory",
        description="Choose the restore location",
        subtype='DIR_PATH',
        update=refresh_restore_versions_on_update,
        default=str(Path.home() / "Documents")
    )

    enable_edit_backup_directory: BoolProperty(
        name="Enable Backup Directory Editing",
        description="Allow editing of the backup directory in the popup",
        default=False,
    )

    enable_edit_restore_directory: BoolProperty(
        name="Enable Restore Directory Editing",
        description="Allow editing of the restore directory in the popup",
        default=False,
    )

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        layout.use_property_decorate = False

        box = layout.box()
        box.label(text="Progress Display", icon='OPTIONS')
        box.prop(self, "progress_display_mode", text="Display Mode")

        box = layout.box()
        box.label(text="Directory Settings", icon='FILE_FOLDER')
        col = box.column()
        col.prop(self, "backup_directory", text="Backup Directory")
        col.prop(self, "restore_directory", text="Restore Directory")

        box = layout.box()
        box.label(text="Allow Directory Editing from Popup", icon='NEWFOLDER')
        box.prop(self, "enable_edit_backup_directory", text="Enable Backup Path Edit")
        box.prop(self, "enable_edit_restore_directory", text="Enable Restore Path Edit")

        box = layout.box()
        box.label(text="Sidebar Panel Category", icon='MENU_PANEL')
        box.prop(self, "category", text="Category")

        box = layout.box()
        box.label(text="Keymap (Popup)", icon="MOUSE_MMB")
        keymap.draw_keymap_ui(box, context)


classes = (
    BLENDER_SYNC_PG_UI_SETTINGS,
    BLENDER_SYNC_PG_folders,
    BLENDER_SYNC_PG_versions,
    BLENDER_REPORT_OT_show_report,
    BLENDER_BACKUP_OT_backup_files,
    BLENDER_RESTORE_OT_restore_files,
    BLENDER_SYNC_OT_sync,
    BLENDER_SYNC_PT_panel,
    BLENDER_SYNC_OT_popup,
    BLENDER_SYNC_preferences
)


def register():
    """Register addon classes and properties."""
    for cls in classes:
        bpy.utils.register_class(cls)
    
    bpy.types.WindowManager.backup_total_size = FloatProperty(
        name="Backup Total Size",
        description="Total size of selected backup files",
        default=0.0,
    )
    bpy.types.Scene.blender_sync_settings = PointerProperty(type=BLENDER_SYNC_PG_UI_SETTINGS)
    bpy.types.Scene.sync_files = PointerProperty(type=BLENDER_SYNC_PG_folders)
    bpy.types.Scene.restore_files = PointerProperty(type=BLENDER_SYNC_PG_folders)
    bpy.types.Scene.backup_files = PointerProperty(type=BLENDER_SYNC_PG_folders)
    bpy.types.Scene.blender_versions = PointerProperty(type=BLENDER_SYNC_PG_versions)

    update_folder_names(None, bpy.context)
    bpy.types.TOPBAR_MT_file.append(draw_menu)
    bpy.types.VIEW3D_HT_tool_header.append(draw_progress_bar)

    prefs = bpy.context.preferences.addons[__package__].preferences
    update_panel_category(prefs)

    keymap.register()
    check_and_show_restore_report()


def unregister():
    """Unregister addon classes and properties."""
    keymap.unregister()
    bpy.types.TOPBAR_MT_file.remove(draw_menu)
    bpy.types.VIEW3D_HT_tool_header.remove(draw_progress_bar)

    try:
        if hasattr(bpy.types.Scene, "blender_sync_settings"):
            del bpy.types.Scene.blender_sync_settings
        if hasattr(bpy.types.Scene, "sync_files"):
            del bpy.types.Scene.sync_files
        if hasattr(bpy.types.Scene, "restore_files"):
            del bpy.types.Scene.restore_files
        if hasattr(bpy.types.Scene, "backup_files"):
            del bpy.types.Scene.backup_files
        if hasattr(bpy.types.Scene, "blender_versions"):
            del bpy.types.Scene.blender_versions
        if hasattr(bpy.types.WindowManager, "backup_total_size"):
            del bpy.types.WindowManager.backup_total_size
    except Exception:
        pass

    for cls in reversed(classes):
        try:
            if hasattr(cls, "bl_rna"):
                bpy.utils.unregister_class(cls)
        except Exception:
            pass