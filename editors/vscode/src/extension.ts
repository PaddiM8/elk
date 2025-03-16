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
            command: "elk",
            args: ["--lsp"],
            transport: TransportKind.stdio,
        },
        debug: {
            command: "elk",
            args: ["--lsp"],
            transport: TransportKind.stdio,
            runtime: "",
        },
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [
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
    client.start();
}

export function deactivate() {
    return client.stop();
}
