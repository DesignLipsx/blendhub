import os
from PIL import Image

def generate_assets(source_path, assets_dir):
    if not os.path.exists(source_path):
        print(f"Source file not found: {source_path}")
        return

    # Asset definitions (filename, width, height)
    assets = [
        ("LockScreenLogo.scale-200.png", 48, 48),
        ("Square44x44Logo.scale-200.png", 88, 88),
        ("Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24),
        ("Square150x150Logo.scale-200.png", 300, 300),
        ("StoreLogo.png", 50, 50),
        ("Wide310x150Logo.scale-200.png", 620, 300),
    ]

    img = Image.open(source_path)
    
    # Handle transparency
    if img.mode != 'RGBA':
        img = img.convert('RGBA')

    for filename, w, h in assets:
        dest_path = os.path.join(assets_dir, filename)
        print(f"Generating {filename} ({w}x{h})...")
        
        # In Wide logo, we center the square logo in a wider canvas
        if w != h:
            new_img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
            # Resize source to fit height
            scale = h / img.height
            resize_w = int(img.width * scale)
            resized = img.resize((resize_w, h), Image.Resampling.LANCZOS)
            # Center it
            offset = (w - resize_w) // 2
            new_img.paste(resized, (offset, 0), resized)
            new_img.save(dest_path)
        else:
            resized = img.resize((w, h), Image.Resampling.LANCZOS)
            resized.save(dest_path)

    print("Asset generation complete.")

if __name__ == "__main__":
    source = r"E:\apps\BlendHub\BlendHub\Assets\logo.png"
    assets_dir = r"E:\apps\BlendHub\BlendHub\Assets"
    generate_assets(source, assets_dir)
