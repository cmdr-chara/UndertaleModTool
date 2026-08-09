using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UndertaleModLib;
using UndertaleModLib.Util;

namespace UndertaleModTests
{
    [TestClass]
    public class CliSafetyTests
    {
        [TestMethod]
        public void NewOverwriteTruncatesExistingFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"umt-new-{Guid.NewGuid():N}.win");
            try
            {
                File.WriteAllBytes(path, new byte[2_000_000]);

                int result = UndertaleModCli.Program.Main(["new", "--output", path, "--overwrite"]);

                Assert.AreEqual(0, result);
                Assert.IsLessThan(2_000_000, new FileInfo(path).Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ReplacementMappingsSplitOnFirstEqualsAndRejectInvalidMappings()
        {
            Assert.IsTrue(UndertaleModCli.Program.TryParseReplacementMappings(
                ["entry=C:\\directory=with-equals\\code.gml"], out Dictionary<string, FileInfo> mappings, out _));
            Assert.AreEqual(@"C:\directory=with-equals\code.gml", mappings["entry"].ToString());

            Assert.IsFalse(UndertaleModCli.Program.TryParseReplacementMappings(["=file.gml"], out _, out _));
            Assert.IsFalse(UndertaleModCli.Program.TryParseReplacementMappings(["entry="], out _, out _));
            Assert.IsFalse(UndertaleModCli.Program.TryParseReplacementMappings(["entry=a", "entry=b"], out _, out _));
        }

        [TestMethod]
        public void ReplaceWithoutOutputFailsBeforeLoadingInput()
        {
            int result = UndertaleModCli.Program.Main(["replace", "missing-data.win"]);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void UniqueTempWriterDoesNotTouchUnrelatedTempAndPreservesOutputOnFailure()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"umt-save-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, "data.win");
            string unrelated = output + "temp";
            File.WriteAllText(output, "original");
            File.WriteAllText(unrelated, "unrelated");
            try
            {
                Assert.Throws<IOException>(() => UndertaleModCli.Program.WriteFileViaUniqueTemp(output, stream =>
                {
                    stream.WriteByte(1);
                    throw new IOException("test failure");
                }));

                Assert.AreEqual("original", File.ReadAllText(output));
                Assert.AreEqual("unrelated", File.ReadAllText(unrelated));
                Assert.HasCount(2, Directory.GetFiles(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void UniqueTempWriterHonorsNoOverwriteAtReplacementTime()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"umt-save-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, "data.win");
            File.WriteAllText(output, "original");
            try
            {
                Assert.Throws<IOException>(() => UndertaleModCli.Program.WriteFileViaUniqueTemp(
                    output,
                    stream => stream.WriteByte(1),
                    overwrite: false));

                Assert.AreEqual("original", File.ReadAllText(output));
                Assert.HasCount(1, Directory.GetFiles(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ScriptCompilationDoesNotExecuteScript()
        {
            string marker = Path.Combine(Path.GetTempPath(), $"umt-lint-{Guid.NewGuid():N}");
            string source = $"System.IO.File.WriteAllText(@\"{marker}\", \"executed\");";

            IReadOnlyList<Diagnostic> diagnostics = UndertaleModCli.Program.CompileUMTScript(
                source, "test.csx", ScriptingUtil.CreateDefaultScriptOptions());

            Assert.IsFalse(File.Exists(marker));
            Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NewStdoutContainsOnlyAValidDataFile(bool verbose)
        {
            List<string> arguments = ["new", "--stdout"];
            if (verbose)
                arguments.Add("--verbose");

            (int exitCode, byte[] stdout, string stderr) = RunCli(arguments);

            Assert.AreEqual(0, exitCode, stderr);
            Assert.IsGreaterThan(0, stdout.Length);
            using MemoryStream stream = new(stdout);
            Assert.IsNotNull(UndertaleIO.Read(stream));
            if (verbose)
                StringAssert.Contains(stderr, "stdout");
            else
                Assert.AreEqual("", stderr);
        }

        [TestMethod]
        public void DumpMissingCodeReturnsFailureWithoutStdoutNoise()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"umt-dump-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string dataPath = Path.Combine(directory, "data.win");
            try
            {
                Assert.AreEqual(0, UndertaleModCli.Program.Main(["new", "--output", dataPath]));

                (int exitCode, byte[] stdout, string stderr) = RunCli(
                    ["dump", dataPath, "--code", "missing_entry", "--output", directory]);

                Assert.AreEqual(1, exitCode);
                Assert.HasCount(0, stdout);
                Assert.IsNotEmpty(stderr);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static (int ExitCode, byte[] Stdout, string Stderr) RunCli(IEnumerable<string> arguments)
        {
            string dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            ProcessStartInfo startInfo = new(dotnetHost)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(typeof(UndertaleModCli.Program).Assembly.Location);
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the CLI process.");
            using MemoryStream stdout = new();
            Task stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(stdoutTask, stderrTask);
            return (process.ExitCode, stdout.ToArray(), stderrTask.Result.Trim());
        }
    }
}
