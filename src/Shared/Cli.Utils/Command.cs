﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using NuGet.Frameworks;

namespace Microsoft.Extensions.Internal
{
    internal class Command
    {
        private readonly Process _process;
        private bool _running = false;
        private Action<string> _stdErrorHandler;
        private Action<string> _stdOutHandler;

        internal static Command CreateDotNet(string commandName, IEnumerable<string> args, NuGetFramework framework = null, string configuration = "Debug")
        {
            return Create(DotNetMuxer.MuxerPathOrDefault(), new[] { commandName }.Concat(args), framework, configuration);
        }

        internal static Command Create(string commandName, IEnumerable<string> args, NuGetFramework framework = null, string configuration = "Debug")
        {
            return new Command(commandName, args, framework, configuration);
        }

        private Command(string commandName, IEnumerable<string> args, NuGetFramework framework, string configuration)
        {
            var psi = new ProcessStartInfo
            {
                FileName = commandName,
                Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args),
                UseShellExecute = false
            };

            _process = new Process
            {
                StartInfo = psi
            };
        }

        public CommandResult Execute()
        {
            ThrowIfRunning();
            _running = true;
            _process.EnableRaisingEvents = true;

            _process.OutputDataReceived += OnOutputReceived;
            _process.ErrorDataReceived += OnErrorReceived;

            _process.Start();

            _process.WaitForExit();

            var exitCode = _process.ExitCode;

            _process.OutputDataReceived -= OnOutputReceived;
            _process.ErrorDataReceived -= OnErrorReceived;

            return new CommandResult(
                _process.StartInfo,
                exitCode);
        }

        private void OnErrorReceived(object sender, DataReceivedEventArgs e)
        {
            _stdErrorHandler?.Invoke(e.Data);
        }

        private void OnOutputReceived(object sender, DataReceivedEventArgs e)
        {
            _stdOutHandler?.Invoke(e.Data);
        }

        public Command OnOutputLine(Action<string> handler)
        {
            ThrowIfRunning();
            _stdOutHandler = handler;
            return this;
        }

        public Command OnErrorLine(Action<string> handler)
        {
            ThrowIfRunning();
            _stdErrorHandler = handler;
            return this;
        }

        private void ThrowIfRunning([CallerMemberName] string memberName = null)
        {
            if (_running)
            {
                throw new InvalidOperationException($"Unable to invoke {memberName} after the command has been run");
            }
        }
    }
}