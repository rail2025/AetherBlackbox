// AetherBlackbox/Core/PageManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBlackbox.DrawingLogic;

namespace AetherBlackbox.Core
{
    public class PageData
    {
        public string Name { get; set; } = "1";

        public List<BaseDrawable> Drawables { get; set; } = new List<BaseDrawable>();
    }

    public class PageManager
    {
        private List<PageData> localPages = new List<PageData>();
        private List<PageData> livePages = new List<PageData>();
        private int currentPageIndex = 0;
        private PageData? pageClipboard = null;
        private PageData? activePageObject = null;
        private static readonly List<BaseDrawable> EmptyDrawablesFallback = new List<BaseDrawable>();

        public bool IsSessionLocked { get; set; } = false;
        public bool IsLiveMode { get; set; } = false;

        public PageManager()
        {
            InitializeDefaultPage();
        }

        public void InitializeDefaultPage()
        {
            if (!localPages.Any())
            {
                localPages.Add(CreateDefaultPage("1"));
                currentPageIndex = 0;
                this.activePageObject = this.GetAllPages().FirstOrDefault();

            }
        }

        private PageData CreateDefaultPage(string name)
        {
            var newPage = new PageData { Name = name };

            float logicalRefCanvasWidth = (850f * 0.75f) - 125f;
            float logicalRefCanvasHeight = 550f;
            Vector2 canvasCenter = new Vector2(logicalRefCanvasWidth / 2f, logicalRefCanvasHeight / 2f);
            
            return newPage;
        }



        public List<PageData> GetAllPages()
        {
            return IsLiveMode ? livePages : localPages;
        }

        public int GetCurrentPageIndex() => currentPageIndex;

        public List<BaseDrawable> GetCurrentPageDrawables()
        {
            var pages = GetAllPages();
            if (pages.Count > 0 && currentPageIndex >= 0 && currentPageIndex < pages.Count)
            {
                return pages[currentPageIndex].Drawables;
            }
            return EmptyDrawablesFallback;
        }

        public void SetCurrentPageDrawables(List<BaseDrawable> drawables)
        {
            var pages = GetAllPages();
            if (pages.Count > 0 && currentPageIndex >= 0 && currentPageIndex < pages.Count)
            {
                pages[currentPageIndex].Drawables = drawables;
            }
            else if (IsLiveMode && pages.Count == 0)
            {
                var newPage = new PageData { Name = "1", Drawables = drawables };
                pages.Add(newPage);
                currentPageIndex = 0;
            }
        }

        public void ClearCurrentPageDrawables()
        {
            var pages = GetAllPages();
            if (pages.Count > 0 && currentPageIndex >= 0 && currentPageIndex < pages.Count)
            {
                pages[currentPageIndex].Drawables.Clear();
                if (pages.Count == 1 && currentPageIndex == 0) pages[currentPageIndex].Name = "1";
            }
        }

    }
}
