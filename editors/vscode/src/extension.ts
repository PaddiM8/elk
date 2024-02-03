import { workspace, ExtensionContext } from "vscode";
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
    Trace
} from "vscode-languageclient/node";

let client: LanguageClient;

export async function activate(context: ExtensionContext) {
    const serverOptions: ServerOptions = {
        run: {
            command: "elk --lsp",
            transport: TransportKind.stdio,
        },
        debug: {
            command: "dotnet",
            args: [
                "run",
                "--project",
                "../../cli/Elk.Cli.csproj",
                "--",
                "--lsp"
            ],
            transport: TransportKind.stdio,
            runtime: "",
        },
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [
            "plaintext",
            {
                pattern: "**/*.elk",
            },
        ],
        progressOnInitialization: true,
        synchronize: {
            configurationSection: "elk-vscode",
            fileEvents: workspace.createFileSystemWatcher("**/*.elk"),
        },
    };

    client = new LanguageClient("elk-vscode", "Elk Language Server", serverOptions, clientOptions);
    client.registerProposedFeatures();
    client.setTrace(Trace.Verbose);
    client.info("hello!")
    client.start();
}

export function deactivate() {
    return client.stop();
}
