using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlendHub.Helpers;
using BlendHub.Models;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Xaml;

namespace BlendHub.Pages
{
    public class BlenderVersionSource : IIncrementalSource<BlenderVersionGroup>
    {
        private readonly List<BlenderVersionGroup> _allVersions;
        private string _searchText;
        private string _sortOption;

        public BlenderVersionSource(List<BlenderVersionGroup> allVersions, string searchText, string sortOption)
        {
            _allVersions = allVersions;
            _searchText = searchText;
            _sortOption = sortOption;
        }

        public async Task<IEnumerable<BlenderVersionGroup>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        {
            // Simulate network delay if needed, but here we are loading from a pre-loaded list
            // In a real app, this might be a database or API call per page
            
            IEnumerable<BlenderVersionGroup> filtered = _allVersions;

            // Apply search filter
            if (!string.IsNullOrEmpty(_searchText))
            {
                var searchLower = _searchText.ToLower();
                filtered = filtered.Where(v =>
                    v.Version.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    v.ShortVersion.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    v.ReleaseDate.Contains(searchLower, StringComparison.OrdinalIgnoreCase));
            }

            // Apply sorting
            filtered = _sortOption switch
            {
                "Version (Newest First)" => filtered.OrderByDescending(v => v.ComparableVersion),
                "Version (Oldest First)" => filtered.OrderBy(v => v.ComparableVersion),
                "Release Date (Newest)" => filtered.OrderByDescending(v => v.ComparableDate),
                "Release Date (Oldest)" => filtered.OrderBy(v => v.ComparableDate),
                _ => filtered
            };

            var result = filtered.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            
            // Artificial delay to show loading state if it's too fast
            if (pageIndex == 0) await Task.Delay(100, cancellationToken);

            return result;
        }
    }
}
