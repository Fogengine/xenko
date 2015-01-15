// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using SiliconStudio.Core;
using SiliconStudio.Paradox.Shaders;

namespace SiliconStudio.Paradox.Assets.Materials
{
    /// <summary>
    /// Smith-GGX Geometric Shadowing.
    /// </summary>
    [DataContract("MaterialSpecularMicrofacetGeometricShadowingSmithGGX")]
    [Display("Smith-GGX")]
    public class MaterialSpecularMicrofacetGeometricShadowingSmithGGX : IMaterialSpecularMicrofacetGeometricShadowingFunction
    {
        public ShaderSource Generate()
        {
            return new ShaderClassSource("MaterialSpecularMicrofacetGeometricShadowingSmithGGX");
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is MaterialSpecularMicrofacetGeometricShadowingSmithGGX;
        }

        public override int GetHashCode()
        {
            return typeof(MaterialSpecularMicrofacetGeometricShadowingSmithGGX).GetHashCode();
        }
    }
}