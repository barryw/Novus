# {{PROJECT_NAME}}

An AmigaOS resident resource targeting 68020 and newer processors.

Build with `novusc build`. Install the resulting `.resource` file where your
loader expects it, then use the generated Novus binding with `OpenResource`.
Resources are permanent once loaded; AmigaOS provides no `CloseResource`.
