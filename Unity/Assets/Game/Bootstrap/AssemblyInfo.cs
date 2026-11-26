using System.Runtime.CompilerServices;

// QA capture, smoke and recording tools (Editor and development builds only) read the bootstrap's internal hooks.
[assembly: InternalsVisibleTo("Mongscape.DevTools")]
