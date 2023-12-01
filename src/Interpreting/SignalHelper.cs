namespace Elk.Interpreting;

public class SignalHelper
{
    public static string[] SignalNames { get; } =
    [
        "SIGHUP (Terminal hung up)",
        "SIGINT (Quit request from job control (^C))",
        "SIGQUIT (Quit request from job control with core dump (^\\))",
        "SIGILL (Illegal instruction)",
        "SIGTRAP (Trace or breakpoint trap)",
        "SIGABRT (Abort)",
        "SIGIOT (Abort (Alias for SIGABRT))",
        "SIGBUS (Misaligned address error)",
        "SIGFPE (Floating point exception)",
        "SIGKILL (Forced quit)",
        "SIGUSR1 (User defined signal 1)",
        "SIGSEGV (Address boundary error)",
        "SIGUSR2 (User defined signal 2)",
        "SIGPIPE (Broken pipe)",
        "SIGALRM (Timer expired)",
        "SIGTERM (Polite quit request)",
        "STKFLT",
        "SIGCHLD (Child process status changed)",
        "SIGCONT (Continue previously stopped process)",
        "SIGSTOP (Forced stop)",
        "SIGTSTP (Stop request from job control (^Z))",
        "SIGTTIN (Stop from terminal input)",
        "SIGTTOU (Stop from terminal output)",
        "SIGURG (Urgent socket condition)",
        "SIGXCPU (CPU time limit exceeded)",
        "SIGXFSZ (File size limit exceeded)",
        "SIGVTALRM (Virtual timefr expired)",
        "SIGPROF (Profiling timer expired)",
        "SIGWINCH (Window size change)",
        "SIGIO (I/O on asynchronous file descriptor is possible)",
    ];
}