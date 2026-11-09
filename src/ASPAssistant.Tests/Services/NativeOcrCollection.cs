namespace ASPAssistant.Tests.Services;

// MaaFramework's native-library resolver mutates a process-wide dictionary at first use.
[CollectionDefinition("Native OCR", DisableParallelization = true)]
public class NativeOcrCollection;
