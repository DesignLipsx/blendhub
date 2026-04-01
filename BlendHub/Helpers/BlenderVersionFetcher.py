"""
Utility for fetching Blender versions JSON from GitHub repository.
This can be used in your C# WinUI application via Python interop or as reference.
"""

import httpx
import asyncio
from typing import Optional, Dict, Any


class BlenderVersionFetcher:
    """Fetches Blender versions from GitHub raw content."""
    
    # GitHub raw content URL
    GITHUB_RAW_URL = "https://raw.githubusercontent.com/DesignLipsx/blendhub/master/BlendHub/blender_versions_web.json"
    TIMEOUT = 10.0
    
    @staticmethod
    async def fetch_versions() -> Optional[Dict[str, Any]]:
        """
        Asynchronously fetch Blender versions JSON from GitHub.
        
        Returns:
            Dictionary with version data or None if fetch fails
        """
        try:
            async with httpx.AsyncClient(timeout=BlenderVersionFetcher.TIMEOUT) as client:
                response = await client.get(BlenderVersionFetcher.GITHUB_RAW_URL)
                response.raise_for_status()
                return response.json()
        except httpx.HTTPError as e:
            print(f"HTTP Error: {e}")
            return None
        except Exception as e:
            print(f"Error fetching versions: {e}")
            return None
    
    @staticmethod
    async def fetch_versions_with_fallback(fallback_data: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """
        Fetch versions with fallback to local data if fetch fails.
        
        Args:
            fallback_data: Local data to use if GitHub fetch fails
            
        Returns:
            Dictionary with version data (from GitHub or fallback)
        """
        versions = await BlenderVersionFetcher.fetch_versions()
        return versions if versions is not None else fallback_data or {}


# Example usage
async def main():
    fetcher = BlenderVersionFetcher()
    versions = await fetcher.fetch_versions()
    
    if versions:
        print("Successfully fetched versions:")
        print(f"Found {len(versions)} entries")
    else:
        print("Failed to fetch versions")


if __name__ == "__main__":
    asyncio.run(main())
