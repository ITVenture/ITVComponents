﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using ITVComponents.Scripting.CScript.Buffering;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.ScriptValues;
using static ITVComponents.Scripting.CScript.Buffering.InterpreterBuffer;

namespace ITVComponents.Scripting.CScript
{
    public class ScriptFile<TOutput>
    {
        private static ConcurrentDictionary<string, Lazy<ScriptFile<TOutput>>> bufferedScripts =
            new ConcurrentDictionary<string, Lazy<ScriptFile<TOutput>>>();

        /// <summary>
        /// the program context for the current script
        /// </summary>
        private ITVScriptingParser.ProgramContext program;

        /// <summary>
        /// the fileName that contains the definition for this script
        /// </summary>
        private string fileName;

        /// <summary>
        /// indicates whether the file is a static script which can only be loaded once
        /// </summary>
        private bool isStatic = false;

        /// <summary>
        /// if static is true, a Stream is expected to hold the script definition
        /// </summary>
        private Stream file = null;

        /// <summary>
        /// Controls the parallel execution and avoids a complete script-reload while other instances are running
        /// </summary>
        private ManualResetEvent executionWait = new ManualResetEvent(true);

        /// <summary>
        /// the instances that are running parallel
        /// </summary>
        private int currentRuns = 0;

        /// <summary>
        /// the last time that the script was parsed
        /// </summary>
        private DateTime lastCompilation;

        /// <summary>
        /// locker object for the lastCompilation date
        /// </summary>
        private object dateLock = new object();

        /// <summary>
        /// indicates whether the script is currently runnable
        /// </summary>
        private bool runnable = false;

        /// <summary>
        /// all errors that were generated by the parser
        /// </summary>
        private string errors;

        private int suspectLine = -1;

        /// <summary>
        /// Lock object used to increase/decrease the current runs counter
        /// </summary>
        private object runCounterLock = new object();

        /// <summary>
        /// Excecutes a Script
        /// </summary>
        /// <param name="variables">the initial variables for the script</param>
        /// <param name="prepareVariables">a callback that will be used in order to initialize lazy-evaluation variable values</param>
        /// <returns>the return value that was generated by the script</returns>
        public TOutput Execute(IDictionary<string, object> variables, InitializeScopeVariables prepareVariables)
        {
            ScriptVisitor visitor;
            InitializeScopeVariables dff = prepareVariables ??
                                               (a =>
                                               {
                                                   DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession);
                                               });
            InitializeScopeVariables newInitializer = args =>
            {
                PreparePowerCalls(args.Scope,prepareVariables,args.Visitor );
                dff(args);
            };
            using (var session = InterpreterBuffer.GetReplInstance(variables, newInitializer, out visitor))
            {
                // todo: reactivate
                return Execute(session);
            }
        }

        /// <summary>
        /// Runs a script inside a specific Scripting context
        /// </summary>
        /// <param name="scriptingContext">the scripting context in which a script is running</param>
        /// <returns>the result of the script</returns>
        public TOutput Execute(IDisposable scriptingContext)
        {
            CheckDate();
            bool ok = false;
            while (!ok)
            {
                lock (runCounterLock)
                {
                    ok = executionWait.WaitOne(100);
                    if (ok)
                    {
                        currentRuns++;
                    }
                }

                if (!ok)
                {
                    executionWait.WaitOne();
                }
            }

            try
            {
                if (!runnable)
                {
                    throw new ScriptException(string.Format("Script is not runnable! Suspect Line: {0}{2}Complete Error-List:{2} {1}", suspectLine, errors, Environment.NewLine));
                }

                if (scriptingContext is RunnerItem rii)
                {
                    var visitor = InterpreterBuffer.GetInterpreter(scriptingContext);
                    ScriptValue retVal = visitor.VisitProgram(program);
                    return ScriptValueHelper.GetScriptValueResult<TOutput>(retVal, false, rii.Policy);
                }

                throw new InvalidOperationException("Interpreter session expected!");
            }
            finally
            {
                lock (runCounterLock)
                {
                    currentRuns--;
                    Monitor.Pulse(runCounterLock);
                }
            }
        }

        /// <summary>
        /// Prepares the PowerCalls (Call and Dict) for using in a Script
        /// </summary>
        /// <param name="variables">the variables that are being prepared</param>
        /// <param name="prepareVariables">an action that is used to prepare a new scope for special methodcalls</param>
        /// <param name="visitor">A visitor instance that is executing the current script and contains the required root variables for child-scripts</param>
        public void PreparePowerCalls(IDictionary<string, object> variables, InitializeScopeVariables prepareVariables, ScriptVisitor visitor)
        {
            variables["Dict"] = new Func<object[], object[], Dictionary<string,object>>((a, b) =>
                Dict(a, b));
            variables["Call"] =
                new Func<string, IDictionary<string,object>, object>((a, b) => CallScript(a, b, prepareVariables, visitor.CopyInitial()));
        }

        /// <summary>
        /// Loads a Scriptfile with the given definition
        /// </summary>
        /// <param name="fileName">the scriptfile containing the source</param>
        /// <returns>a script-instance that contains the requested script - definition</returns>
        public static ScriptFile<TOutput> FromFile(string fileName)
        {
            var scriptBuffer = bufferedScripts.GetOrAdd(fileName,
                s => new Lazy<ScriptFile<TOutput>>(() =>
                {
                    var ret = new ScriptFile<TOutput> {fileName = fileName}; 
                    ret.ReloadDefinition();
                    return ret;
                }));
            //ScriptFile<TOutput> retVal = new ScriptFile<TOutput>() {fileName = fileName};
            return scriptBuffer.Value;
        }

        /// <summary>
        /// Loads a Scriptfile with the given definition
        /// </summary>
        /// <param name="file">the stream containing the source</param>
        /// <returns>a script-instance that contains the requested script - definition</returns>
        public static ScriptFile<TOutput> FromStream(Stream file)
        {
            ScriptFile<TOutput> retVal = new ScriptFile<TOutput>() {file = file, isStatic=true};
            retVal.ReloadDefinition();
            return retVal;
        }

        /// <summary>
        /// Loads a Scriptfile from the given script-text
        /// </summary>
        /// <param name="scriptText">the script as text-source</param>
        /// <returns>a script-instance that contains the requested script - definition</returns>
        public static ScriptFile<TOutput> FromText(string scriptText)
        {
            var raw = Encoding.Default.GetBytes(scriptText);
            var stm = new MemoryStream(raw);
            stm.Seek(0, SeekOrigin.Begin);
            return FromStream(stm);
        }

        /// <summary>
        /// Checks the script file for changes since the last parse
        /// </summary>
        private void CheckDate()
        {
            if (!isStatic)
            {
                if (Monitor.TryEnter(dateLock, 100))
                {
                    try
                    {
                        FileInfo f = new FileInfo(fileName);
                        if (f.Exists && f.LastWriteTime > lastCompilation)
                        {
                            ReloadDefinition();
                        }
                    }
                    finally
                    {
                        Monitor.Exit(dateLock);
                    }
                }
            }
        }
 
        /// <summary>
        /// Reloads the definition of the program
        /// </summary>
        private void ReloadDefinition()
        {
            lock (runCounterLock)
            {
                executionWait.Reset();
                while (currentRuns > 0)
                {
                    Monitor.Wait(runCounterLock);
                }
            }

            try
            {
                AntlrInputStream astr = null;
                if (!isStatic)
                {
                    astr = new AntlrFileStream(fileName, Encoding.Default);
                }
                else
                {
                    using (var r = new StreamReader(file, Encoding.Default))
                    {
                        astr = new AntlrInputStream(r);
                    }
                }

                //SingletonPredictionContext.
                Lexer lex = new ITVScriptingLexer(astr);
                ITVScriptingParser parser = new ITVScriptingParser(new CommonTokenStream(lex));
                ErrorListener listener = new ErrorListener();
                parser.RemoveErrorListeners();
                parser.AddErrorListener(listener);
                //parser.AddParseListener(new ScriptErrorHandler());
                program = parser.program();
                runnable = parser.NumberOfSyntaxErrors == 0;
                suspectLine = listener.SuspectLine;
                errors = listener.GetAllErrors();
                lastCompilation = DateTime.Now;
            }
            finally
            {
                executionWait.Set();
            }
        }

        /// <summary>
        /// Calls a Script inside a script
        /// </summary>
        /// <param name="scriptFile">the script file</param>
        /// <param name="initialVariables">the initial variables</param>
        /// <param name="prepareVariables">a value preparer for root values</param>
        /// <param name="baseValues">the initial values that are contained in the root-script</param>
        /// <returns>the value of the script</returns>
        private static object CallScript(string scriptFile, IDictionary<string, object> initialVariables, InitializeScopeVariables prepareVariables, IDictionary<string, object> baseValues)
        {
            bool dontClear = initialVariables is IScope;
            IDictionary<string, object> construct = (initialVariables is IScope)
                ? initialVariables
                : new Dictionary<string, object>(initialVariables);
            try
            {
                foreach (KeyValuePair<string, object> tmp in baseValues)
                {
                    if (!construct.ContainsKey(tmp.Key))
                    {
                        construct[tmp.Key] = tmp.Value;
                    }
                }

                ScriptFile<object> file = ScriptFile<object>.FromFile(scriptFile);
                return file.Execute(construct, prepareVariables);
            }
            finally
            {
                if (!dontClear)
                {
                    construct.Clear();
                }
            }
        }

        /// <summary>
        /// Creates a dictionary that can be used as scope
        /// </summary>
        /// <param name="keys">the variable names</param>
        /// <param name="values">the variable values</param>
        
        /// <returns>the generated dictionary</returns>
        private static Dictionary<string, object> Dict(object[] keys, object[] values)
        {
            Dictionary<string,object> retVal = new Dictionary<string, object>();
            for (int i = 0; i < keys.Length; i++)
            {
                retVal.Add((string)keys[i],values[i]);
            }

            return retVal;
        }
    }
}
