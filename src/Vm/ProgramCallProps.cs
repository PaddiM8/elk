using System;

namespace Elk.Vm;

[Flags]
enum ProgramCallProps : ushort
{
    None = 0,
    HasPipedValue = 1,
    DisableRedirectionBuffering = 2,
    AutomaticStart = 4,
    RedirectOutput = 8,
    RedirectError = 16,
}