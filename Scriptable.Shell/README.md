## API Overview

### Commands

The `Command` class represents an executing process:
```C#
// create a command via Command.Run
var command = Command.Run("executable", "arg1", "arg2", ...);

// wait for it to finish
command.Wait(); // or...
var result = command.Result; // or...
result = await command.Task;

// inspect the result
if (!result.Success)
{
    Console.Error.WriteLine($"command failed with exit code {result.ExitCode}: {result.StandardError}");
}
```
The `Command.Task` property means that you can easily compose the `Command`'s execution with other `Task`-based async operations. You can terminate a `Command` by invoking its `Kill()` method.

Most APIs create a `Command` instance by starting a new process. However, you can also create a `Command` from an existing process via the `Command.TryAttachToProcess` API.

### Standard IO

One of the main ways to interact with a process is via its [standard IO streams](https://en.wikipedia.org/wiki/Standard_streams) (in, out and error). By default, MedallionShell configures the process to enable these streams and captures standard error and standard output in the `Command`'s result.

Additionally/alternatively, you can interact with these streams directly via the `Command.StandardInput`, `Command.StandardOutput`, and `Command.StandardError` properties. As with `Process`, these are `TextWriter`/`TextReader` objects that also expose the underlying `Stream`, giving you the option of writing/reading either text or raw bytes.

The standard IO streams also contain methods for piping to and from common sinks and sources, including `Stream`s, `TextReader/Writer`s, files, and collections. For example:
```C#
command.StandardInput.PipeFromAsync(new FileInfo("input.csv")); // pipes in all bytes from input.csv
var outputLines = new List<string>();
command.StandardOutput.PipeToAsync(outputLines); // pipe output text to a collection
```

You can also express piping directly on the `Command` object. This returns a new `Command` instance which represents both the underlying process execution and the IO piping operation, providing one thing you can await to know when everything has completed. You can even use this feature to chain together commands (like the `|` operator on the command line).
```C#
await Command.Run("processingStep1.exe")
	.RedirectFrom(new FileInfo("input.txt"))
	.PipeTo(Command.Run("processingStep2.exe"))
	.RedirectTo(new FileInfo("output.txt"));
	
// alternatively, this can be expressed with operators as on the command line
await Command.Run("ProcssingStep1.exe") < new FileInfo("input.txt")
	| Command.Run("processingStep2.exe") > new FileInfo("output.text");
```

### Stopping a Command

You can immediately terminate a command with the `Kill()` API. You can also use the `TrySignalAsync` API to send other types of signals which can allow for graceful shutdown if the target process handles them. `CommandSignal.ControlC` works across platforms, while other signals are OS-specific.

### Command Options

When constructing a `Command`, you can specify various options to provide additional configuration:
```C#
Command.Run("foo.exe", new[] { "arg1" }, options => options.ThrowOnError()...);
```

The supported options are:

|Option|Description|Default|
| --- | --- | --- |
|**ThrowOnError**|If true, the command will throw an exception if the underlying process returns a non-zero exit code rather than returning a failed result|`false`|
|**WorkingDirectory**|Sets the initial working directory for the process|`Environment.CurrentDirectory`|
|**CancellationToken**|Specifies a `CancellationToken` which will kill the process if canceled|`CancellationToken.None`|
|**Timeout**|Specifies a time period after which the process will be killed|`Timeout.Infinite`|
|**StartInfo**|Specifies arbitrary additional configuration of the `ProcessStartInfo` object| |
|**DisposeOnExit**|If true, the underlying `Process` object will be disposed when the process exits, removing the need to call `Command.Dispose()`|`true`|
|**EnvironmentVariable(s)**|Specifies environment variable overrides for the process|`Environment.GetEnvironmentVariables()`|
|**Encoding**|Specifies an `Encoding` to be used on all standard IO streams|`Console.OutputEncoding`/`Console.InputEncoding`: note that what this is varies by platform!|
|**Command**|Specifies arbitrary additional configuration of the `Command` object after it is created (generally only useful with `Shell`s, which are described below) | |

### Shells
It is frequently the case that within the context of a single application all the `Command`s you invoke will want the same or very similar options. To simplify this, you can package up a set of options in a `Shell` object for convenient re-use:
```C#
private static readonly Shell MyShell = new Shell(options => options.ThrowOnError().Timeout(...)...);

...

var command = MyShell.Run("foo.exe", new[] { "arg1", ... }, options => /* can still override/specify further options */);
```
