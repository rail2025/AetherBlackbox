using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace AetherBlackbox.Core.Mechanics
{
    public class RenderedMechanicInstance
    {
        public Guid UniqueId { get; set; } = Guid.NewGuid();
        public float ExecutionTimestamp { get; set; }
        public Vector3 WorldPosition { get; set; }
        public float Rotation { get; set; }
        public CustomMechanicEntry Template { get; set; }

        public RenderedMechanicInstance(CustomMechanicEntry template)
        {
            Template = template;
        }
    }
}
