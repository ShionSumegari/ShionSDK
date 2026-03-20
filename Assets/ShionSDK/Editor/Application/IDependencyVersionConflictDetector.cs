using System.Collections.Generic;
using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public interface IDependencyVersionConflictDetector
    {
        List<DepVersionConflict> GetConflicts(Module root);
    }
}