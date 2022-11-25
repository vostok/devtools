An msbuild task that check references during build process.
Features:
1. Restrict some references(dll, nuget, csproj)
2. Restrict ProjectReferences outside of solution


To install it into any project, just include the .props file directly into .csproj:

<Import Project="<path to props file relative to target csproj>" />

It may look like this in a project that resides in a subdirectory of its cement module:

<Import Project="..\..\vostok.devtools\ref-analyzer\RefAnalyzer.props" />

Then set desired properties:
<RefAnalyzerForbiddingRegexp>SomeLibRegex</RefAnalyzerForbiddingRegexp>
<RefAnalyzerOwner>who to ask about restrictions</RefAnalyzerOwner>
<RefAnalyzerCheckSolutionBoundary></RefAnalyzerCheckSolutionBoundary>
