using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
