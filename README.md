# ITVComponents
ITVenture Toolset

ITVComponents is a Set of libraries for the .net framework it consists of the following features

- Scripting
  * The Scripting engine consists of an interpreter and is also capable of executing parts of your script native. The Syntax is derived from javascript with some additions for type-loading and configuring/executing native code-parts.
- PlugIn Framework
  * The Plugin-Framework allows you to load custom modules. The plugins can be defined in the configuration or in a database-table. 
  * There are bootstrap-libraries that siplify the usage of the Plugin-engine in Service projects as well as GUI applications
  * the WebCoreToolkit provides DI-extensions that enable you to configure your DI-Modules in a database or in a config-file
- Formatting
  * The Formatting feature makes use of the Scripting-engine and enables you to use extended formatting, whenever you can not use string-interpolation for some reason.
  * Formatting-blocks can be recursive and can also contain entire script-blocks
  * If you have enabled encryption in ITVComponents.PasswordSecurity, you can also use it to decrypt strings
  * There is an extension-plugin, that enables the PluginFactory to use Formatting for Plugin-Parameter strings. This enables you to keep passwords in constructors encrypted.
- data import / export features
  * Import Data from Excel, CSV, or custom sources
  * Use Constraints to define, which Import-Records are required for further processing
- InterProcess Communication
  * Communicate with objects located in Service-Processes using GRPC
  * A Proxy-Layer allows you to use objects of the external Process as if they were part of your local application.
. AspNet Core Extensions in WebCoreToolkit
  * Inject configured plugins as services
  * Inject IPC-Proxies as services
  * Multi-Tenant Stub allows you to build a multi-tenant system easily. It supports per-tenant Plugins, Settings, Constants (which can be used as Plugin-parameters)
  * Configurable Diagnostics-queries which can be queried over one unified interface and return a JSON-response
  * Unified Foreign-Key interface for all attached database-contexts (as plugin or as normal injected service)
  
  
Supports .Net Core 3.1 and .Net 5 in Production branch
