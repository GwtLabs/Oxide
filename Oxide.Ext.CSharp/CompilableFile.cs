﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Oxide.Core;

namespace Oxide.Plugins
{
    public class CompilableFile
    {
        private static Core.Libraries.Timer timer = Interface.Oxide.GetLibrary<Core.Libraries.Timer>();
        private static object compileLock = new object();

        public CSharpExtension Extension;
        public CSharpPluginLoader Loader;
        public string Name;
        public string Directory;
        public string ScriptName;
        public string ScriptPath;
        public string[] ScriptLines;
        public Encoding ScriptEncoding;
        public HashSet<string> Requires = new HashSet<string>();
        public HashSet<string> References = new HashSet<string>();
        public HashSet<string> IncludePaths = new HashSet<string>();
        public string CompilerErrors;
        public CompiledAssembly CompiledAssembly;
        public DateTime LastModifiedAt;
        public DateTime LastCachedScriptAt;
        public DateTime LastCompiledAt;
        public bool IsCompilationNeeded;

        protected Action<CSharpPlugin> loadCallback;
        protected Action<bool> compileCallback;
        protected float compilationQueuedAt;

        private Core.Libraries.Timer.TimerInstance timeoutTimer;

        public byte[] ScriptSource => ScriptEncoding.GetBytes(string.Join(Environment.NewLine, ScriptLines));

        public CompilableFile(CSharpExtension extension, CSharpPluginLoader loader, string directory, string name)
        {
            Extension = extension;
            Loader = loader;
            Directory = directory;
            ScriptName = name;
            Name = Regex.Replace(Regex.Replace(ScriptName, @"(?:^|_)([a-z])", m => m.Groups[1].Value.ToUpper()), "_", "");
            ScriptPath = Path.Combine(Directory, string.Format("{0}.cs", ScriptName));
            CheckLastModificationTime();
        }

        internal void Compile(Action<bool> callback)
        {
            lock (compileLock)
            {
                if (compilationQueuedAt > 0f)
                {
                    var ago = Interface.Oxide.Now - compilationQueuedAt;
                    Interface.Oxide.LogDebug($"Plugin compilation is already queued: {ScriptName} ({ago:0.000} ago)");
                    //RemoteLogger.Debug($"Plugin compilation is already queued: {ScriptName} ({ago:0.000} ago)");
                    return;
                }
                OnLoadingStarted();
                if (CompiledAssembly != null && !HasBeenModified())
                {
                    if (CompiledAssembly.IsLoading || !CompiledAssembly.IsBatch || CompiledAssembly.CompilablePlugins.All(pl => pl.IsLoading))
                    {
                        //Interface.Oxide.LogDebug("Plugin is already compiled: {0}", Name);
                        callback(true);
                        return;
                    }
                }
                IsCompilationNeeded = true;
                compileCallback = callback;
                compilationQueuedAt = Interface.Oxide.Now;
                OnCompilationRequested();
            }
        }
        
        internal virtual void OnCompilationStarted()
        {
            //Interface.Oxide.LogDebug("Compiling plugin: {0}", Name);
            LastCompiledAt = LastModifiedAt;
            timeoutTimer?.Destroy();
            timeoutTimer = null;
            Interface.Oxide.NextTick(() =>
            {
                timeoutTimer?.Destroy();
                timeoutTimer = timer.Once(60f, OnCompilationTimeout);
            });
        }

        internal void OnCompilationSucceeded(CompiledAssembly compiled_assembly)
        {
            if (timeoutTimer == null)
            {
                Interface.Oxide.LogWarning($"Ignored unexpected plugin compilation: {Name}");
                return;
            }
            timeoutTimer?.Destroy();
            timeoutTimer = null;
            IsCompilationNeeded = false;
            compilationQueuedAt = 0f;
            CompiledAssembly = compiled_assembly;
            compileCallback?.Invoke(true);
        }

        internal void OnCompilationFailed()
        {
            if (timeoutTimer == null)
            {
                Interface.Oxide.LogWarning($"Ignored unexpected plugin compilation failure: {Name}");
                return;
            }
            timeoutTimer?.Destroy();
            timeoutTimer = null;
            compilationQueuedAt = 0f;
            LastCompiledAt = default(DateTime);
            compileCallback?.Invoke(false);
            IsCompilationNeeded = false;
        }

        internal void OnCompilationTimeout()
        {
            Interface.Oxide.LogError("Timed out waiting for plugin to be compiled: " + Name);
            CompilerErrors = "Timed out waiting for compilation";
            OnCompilationFailed();
        }

        internal bool HasBeenModified()
        {
            var last_modified_at = LastModifiedAt;
            CheckLastModificationTime();
            return LastModifiedAt != last_modified_at;
        }

        internal void CheckLastModificationTime()
        {
            if (!File.Exists(ScriptPath))
            {
                LastModifiedAt = default(DateTime);
                return;
            }
            var modified_time = GetLastModificationTime();
            if (modified_time != default(DateTime)) LastModifiedAt = modified_time;
        }

        internal DateTime GetLastModificationTime()
        {
            try
            {
                return File.GetLastWriteTime(ScriptPath);
            }
            catch (IOException ex)
            {
                Interface.Oxide.LogError("IOException while checking plugin: {0} ({1})", ScriptName, ex.Message);
                return default(DateTime);
            }
        }
        
        protected virtual void OnLoadingStarted()
        {
        }

        protected virtual void OnCompilationRequested()
        {
        }

        protected virtual void InitFailed(string message = null)
        {
            if (message != null) Interface.Oxide.LogError(message);
            if (loadCallback != null) loadCallback(null);
        }
    }
}
