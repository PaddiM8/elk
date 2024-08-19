namespace Elk.Vm;

public class SignalHelper
{
    public static string[] SignalNames { get; } =
    [
        "", // SIGHUP
        "", // SIGINT
        "", // SIGQUIT
        "SIGILL (Illegal instruction)",
        "SIGTRAP (Trace or breakpoint trap)",
        "", // SIGABRT
        "", // SIGIOT
        "SIGBUS (Misaligned address error)",
        "SIGFPE (Floating point exception)",
        "SIGKILL (Forced quit)",
        "", // SIGUSR1
        "SIGSEGV (Address boundary error)",
        "", // SIGUSR2
        "", // SIGPIPE
        "", // SIGALARM
        "", // SIGTERM
        "", // STKFLT
        "", // SIGCHLD
        "", // SIGCONT
        "", // SIGSTOP
        "", // SIGTSTP
        "", // SIGTTIN
        "", //SIGTTOU
        "SIGURG (Urgent socket condition)",
        "SIGXCPU (CPU time limit exceeded)",
        "SIGXFSZ (File size limit exceeded)",
        "SIGVTALRM (Virtual timefr expired)",
        "SIGPROF (Profiling timer expired)",
        "", // SIGWINCH
        "", // SIGIO
    ];
}