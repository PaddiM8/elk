using System.Collections;
using System.Collections.Generic;

namespace Elk.DocGen;

public record StdInfo(IEnumerable<ModuleInfo> Modules, IEnumerable<FunctionInfo> GlobalFunctions);