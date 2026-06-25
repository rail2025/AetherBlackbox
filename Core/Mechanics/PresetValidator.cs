using System;
using System.Collections.Generic;
using System.Text;

namespace AetherBlackbox.Core.Mechanics
{
    public static class PresetValidator
    {
        public static bool IsValid(CustomMechanicEntry entry)
        {
            if (entry == null) return false;
            if (entry.Version < 1) return false;

            if (entry.Radius < 0 || entry.Radius > 200) return false;
            if (entry.InnerRadius < 0 || entry.InnerRadius >= entry.Radius) return false;
            if (entry.Width < 0 || entry.Width > 200) return false;
            if (entry.Thickness < 0 || entry.Thickness > 50) return false;
            if (entry.Duration <= 0 || entry.Duration > 60) return false;

            return true;
        }
    }
}