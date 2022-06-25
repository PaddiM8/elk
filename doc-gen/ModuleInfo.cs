using System.Collections.Generic;

namespace Elk.DocGen;

public record ModuleInfo(string Name, string DisplayName, IEnumerable<FunctionInfo> Functions);