const { LanguageClient, TransportKind } = require('vscode-languageclient/node');
const path = require('path');

let client;

function activate(context) {
    const proj = path.join(__dirname, '..', 'tql-lsp.csproj');

    const serverOptions = {
        command: 'dotnet',
        args: ['run', '--project', proj, '-c', 'Release'],
        transport: TransportKind.stdio
    };

    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'tql' }]
    };

    client = new LanguageClient('tql-lsp', 'TQL Language Server', serverOptions, clientOptions);
    client.start();
    context.subscriptions.push(client);
}

function deactivate() {
    return client?.stop();
}

module.exports = { activate, deactivate };
