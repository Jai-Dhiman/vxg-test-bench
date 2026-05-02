using System.CommandLine;
using System.CommandLine.Parsing;
using VirtualVxg.Simulator;

var configOption = new Option<string>("--config") { Required = true };
var portOption = new Option<int>("--port") { DefaultValueFactory = _ => 5025 };

var root = new RootCommand("Virtual Keysight M9484C VXG simulator");
root.Add(configOption);
root.Add(portOption);

root.SetAction(async (ParseResult result, CancellationToken ct) =>
{
    var configPath = result.GetValue(configOption)!;
    var port = result.GetValue(portOption);

    var config = UnitConfig.Load(configPath);
    var state = new InstrumentState();
    var defects = new DefectEngine(config);
    var handler = new ScpiCommandHandler(state, defects);
    var server = new ScpiServer(handler);

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    await server.StartAsync(port, cts.Token);
    Console.WriteLine($"VXG simulator listening on tcp://127.0.0.1:{port} (unit: {config.UnitId})");
    try { await Task.Delay(Timeout.Infinite, cts.Token); }
    catch (OperationCanceledException) { }
    await server.StopAsync();
});

var parseResult = CommandLineParser.Parse(root, args);
return await parseResult.InvokeAsync();
