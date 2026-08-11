import * as path from 'path';
import { workspace, ExtensionContext, window } from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
    console.log('Novus extension activating...');

    // Path to the language server executable
    const serverPath = findServerPath(context.extensionPath);

    if (!serverPath) {
        console.error('Novus Language Server not found!');
        window.showErrorMessage('Novus Language Server is missing. Reinstall the Novus extension.');
        return;
    }

    console.log('Found Novus Language Server at:', serverPath);

    // Configure server launch
    const serverOptions: ServerOptions = {
        run: { command: serverPath, transport: TransportKind.stdio },
        debug: { command: serverPath, transport: TransportKind.stdio }
    };

    // Configure client options
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'novus' }],
        synchronize: {
            fileEvents: workspace.createFileSystemWatcher('**/*.novus')
        }
    };

    // Create and start the language client
    client = new LanguageClient(
        'novusLanguageServer',
        'Novus Language Server',
        serverOptions,
        clientOptions
    );

    client.start();
    console.log('Novus Language Server started!');
}

export function deactivate(): Thenable<void> | undefined {
    if (!client) {
        return undefined;
    }
    return client.stop();
}

function findServerPath(extensionPath: string): string | null {
    // The bundled server and stdlib are built from the same source revision. A
    // development checkout may use its current net10 build before packaging.
    const possiblePaths = [
        path.join(extensionPath, 'server', 'Novus.LanguageServer'),
        path.join(extensionPath, '..', 'Novus.LanguageServer', 'bin', 'Debug', 'net10.0', 'Novus.LanguageServer'),
        path.join(extensionPath, '..', 'Novus.LanguageServer', 'bin', 'Release', 'net10.0', 'Novus.LanguageServer'),
    ];

    for (const serverPath of possiblePaths) {
        try {
            const fs = require('fs');
            const executable = fs.existsSync(serverPath) ? serverPath : serverPath + '.exe';
            if (fs.existsSync(executable)) {
                console.log(`Found Novus Language Server at: ${executable}`);
                return executable;
            }
        } catch (e) {
            // Continue searching
        }
    }

    return null;
}
