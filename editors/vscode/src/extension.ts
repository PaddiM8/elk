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
            options: { shell: true }
        },
        debug: {
            command: "elk",
            args: ["--lsp"],
            transport: TransportKind.stdio,
            runtime: "",
            options: { shell: true }
        },
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [
            { scheme: "file", language: "elk" },
            { scheme: "untitled", language: "elk" },
        ],
        progressOnInitialization: true,
        synchronize: {
            configurationSection: "elk-vscode",
            fileEvents: workspace.createFileSystemWatcher("**/*"),
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
