using System.Runtime.CompilerServices;

// Bringup des Interpreter-Patterns: Der neue Ausfuehrungsbaum lebt in einer eigenen Assembly,
// braucht aber dieselbe Runtime-Semantik wie der ScriptVisitor. Statt MemberAccessHelper,
// ScriptValueHelper und die Policy-Zugriffe des Scopes divergent nachzubauen, bekommt das
// Interpreter-Projekt Zugriff auf die internen Member.
// Faellt weg, sobald der Interpreter-Code nach ITVComponents.Scripting.CScript zurueckfliesst.
[assembly: InternalsVisibleTo("ITVComponents.Scripting.CScript.Interpreter")]
