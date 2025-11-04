import * as path from 'path';
import { workspace, ExtensionContext } from 'vscode';
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
    const serverPath = findServerPath();

    if (!serverPath) {
        console.error('Novus Language Server not found!');
        console.error('Searched paths:', [
            path.join(__dirname, '../../Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer'),
            path.join(__dirname, '../../Novus.LanguageServer/bin/Release/net9.0/Novus.LanguageServer'),
            path.join(__dirname, '../server/Novus.LanguageServer'),
        ]);
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

function findServerPath(): string | null {
    // Try to find the language server executable
    // Look in the extension's directory structure
    const possiblePaths = [
        // Absolute path to development server
        '/Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer',
        '/Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Release/net9.0/Novus.LanguageServer',
        // Development - when extension is in the Novus repo
        path.join(__dirname, '../../Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer'),
        path.join(__dirname, '../../Novus.LanguageServer/bin/Release/net9.0/Novus.LanguageServer'),
        // Installed extension - bundled server
        path.join(__dirname, '../server/Novus.LanguageServer'),
    ];

    for (const serverPath of possiblePaths) {
        try {
            const fs = require('fs');
            if (fs.existsSync(serverPath) || fs.existsSync(serverPath + '.exe')) {
                console.log(`Found Novus Language Server at: ${serverPath}`);
                return serverPath;
            }
        } catch (e) {
            // Continue searching
        }
    }

    return null;
}
