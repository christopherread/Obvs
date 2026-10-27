; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
OBVS001 | Obvs     | Warning  | ServiceConfigurationAnalyzer: a message registered or handled on a service doesn't implement its message interface
OBVS002 | Obvs     | Warning  | MessageKindAnalyzer: a message implements more than one of ICommand, IEvent, IRequest and IResponse
OBVS003 | Obvs     | Warning  | ServiceConfigurationAnalyzer: a request handler's response doesn't implement the service's message interface
OBVS004 | Obvs     | Warning  | ServiceConfigurationAnalyzer: a service has no transport
OBVS005 | Obvs     | Warning  | SubscriptionAnalyzer: the subscription to a bus stream is discarded
OBVS006 | Obvs     | Warning  | SubscriptionAnalyzer: an async lambda subscribes to a bus stream, as async void
