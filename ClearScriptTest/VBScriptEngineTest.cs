﻿// 
// Copyright (c) Microsoft Corporation. All rights reserved.
// 
// Microsoft Public License (MS-PL)
// 
// This license governs use of the accompanying software. If you use the
// software, you accept this license. If you do not accept the license, do not
// use the software.
// 
// 1. Definitions
// 
//   The terms "reproduce," "reproduction," "derivative works," and
//   "distribution" have the same meaning here as under U.S. copyright law. A
//   "contribution" is the original software, or any additions or changes to
//   the software. A "contributor" is any person that distributes its
//   contribution under this license. "Licensed patents" are a contributor's
//   patent claims that read directly on its contribution.
// 
// 2. Grant of Rights
// 
//   (A) Copyright Grant- Subject to the terms of this license, including the
//       license conditions and limitations in section 3, each contributor
//       grants you a non-exclusive, worldwide, royalty-free copyright license
//       to reproduce its contribution, prepare derivative works of its
//       contribution, and distribute its contribution or any derivative works
//       that you create.
// 
//   (B) Patent Grant- Subject to the terms of this license, including the
//       license conditions and limitations in section 3, each contributor
//       grants you a non-exclusive, worldwide, royalty-free license under its
//       licensed patents to make, have made, use, sell, offer for sale,
//       import, and/or otherwise dispose of its contribution in the software
//       or derivative works of the contribution in the software.
// 
// 3. Conditions and Limitations
// 
//   (A) No Trademark License- This license does not grant you rights to use
//       any contributors' name, logo, or trademarks.
// 
//   (B) If you bring a patent claim against any contributor over patents that
//       you claim are infringed by the software, your patent license from such
//       contributor to the software ends automatically.
// 
//   (C) If you distribute any portion of the software, you must retain all
//       copyright, patent, trademark, and attribution notices that are present
//       in the software.
// 
//   (D) If you distribute any portion of the software in source code form, you
//       may do so only under this license by including a complete copy of this
//       license with your distribution. If you distribute any portion of the
//       software in compiled or object code form, you may only do so under a
//       license that complies with this license.
// 
//   (E) The software is licensed "as-is." You bear the risk of using it. The
//       contributors give no express warranties, guarantees or conditions. You
//       may have additional consumer rights under your local laws which this
//       license cannot change. To the extent permitted under your local laws,
//       the contributors exclude the implied warranties of merchantability,
//       fitness for a particular purpose and non-infringement.
//       

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.ClearScript.Util;
using Microsoft.ClearScript.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ClearScript.Test
{
    [TestClass]
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Test classes use TestCleanupAttribute for deterministic teardown.")]
    public class VBScriptEngineTest : ClearScriptTest
    {
        #region setup / teardown

        private VBScriptEngine engine;

        [TestInitialize]
        public void TestInitialize()
        {
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging);
            engine.Execute("function pi : pi = 4 * atn(1) : end function");
            engine.Execute("function e : e = exp(1) : end function");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            engine.Dispose();
        }

        #endregion

        #region test methods

        // ReSharper disable InconsistentNaming

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostObject()
        {
            var host = new HostFunctions();
            engine.AddHostObject("host", host);
            Assert.AreSame(host, engine.Evaluate("host"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void VBScriptEngine_AddHostObject_Scalar()
        {
            engine.AddHostObject("value", 123);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostObject_Enum()
        {
            const DayOfWeek value = DayOfWeek.Wednesday;
            engine.AddHostObject("value", value);
            Assert.AreEqual(value, engine.Evaluate("value"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostObject_Struct()
        {
            var date = new DateTime(2007, 5, 22, 6, 15, 43);
            engine.AddHostObject("date", date);
            Assert.AreEqual(date, engine.Evaluate("date"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostObject_GlobalMembers()
        {
            var host = new HostFunctions();
            engine.AddHostObject("host", HostItemFlags.GlobalMembers, host);
            Assert.IsInstanceOfType(engine.Evaluate("newObj()"), typeof(PropertyBag));

            engine.AddHostObject("test", HostItemFlags.GlobalMembers, this);
            engine.Execute("TestProperty = newObj()");
            Assert.IsInstanceOfType(TestProperty, typeof(PropertyBag));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        [ExpectedException(typeof(ScriptEngineException))]
        public void VBScriptEngine_AddHostObject_DefaultAccess()
        {
            engine.AddHostObject("test", this);
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostObject_PrivateAccess()
        {
            engine.AddHostObject("test", HostItemFlags.PrivateAccess, this);
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddRestrictedHostObject_BaseClass()
        {
            var host = new ExtendedHostFunctions() as HostFunctions;
            engine.AddRestrictedHostObject("host", host);
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj()"), typeof(PropertyBag));
            TestUtil.AssertException<ScriptEngineException>(() => engine.Evaluate("host.type(\"System.Int32\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddRestrictedHostObject_Interface()
        {
            const double value = 123.45;
            engine.AddRestrictedHostObject("convertible", value as IConvertible);
            engine.AddHostObject("culture", CultureInfo.InvariantCulture);
            Assert.AreEqual(value, engine.Evaluate("convertible.ToDouble(culture)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("Random", typeof(Random));
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(Random)"), typeof(Random));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_GlobalMembers()
        {
            engine.AddHostType("Guid", HostItemFlags.GlobalMembers, typeof(Guid));
            Assert.IsInstanceOfType(engine.Evaluate("NewGuid()"), typeof(Guid));

            engine.AddHostType("Test", HostItemFlags.GlobalMembers, GetType());
            engine.Execute("StaticTestProperty = NewGuid()");
            Assert.IsInstanceOfType(StaticTestProperty, typeof(Guid));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        [ExpectedException(typeof(ScriptEngineException))]
        public void VBScriptEngine_AddHostType_DefaultAccess()
        {
            engine.AddHostType("Test", GetType());
            engine.Execute("Test.PrivateStaticMethod()");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_PrivateAccess()
        {
            engine.AddHostType("Test", HostItemFlags.PrivateAccess, GetType());
            engine.Execute("Test.PrivateStaticMethod()");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_Static()
        {
            engine.AddHostType("Enumerable", typeof(Enumerable));
            Assert.IsInstanceOfType(engine.Evaluate("Enumerable.Range(0, 5).ToArray()"), typeof(int[]));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_OpenGeneric()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("List", typeof(List<>));
            engine.AddHostType("Guid", typeof(Guid));
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(List(Guid))"), typeof(List<Guid>));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_ByName()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("Random", "System.Random");
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(Random)"), typeof(Random));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_ByNameWithAssembly()
        {
            engine.AddHostType("Enumerable", "System.Linq.Enumerable", "System.Core");
            Assert.IsInstanceOfType(engine.Evaluate("Enumerable.Range(0, 5).ToArray()"), typeof(int[]));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddHostType_ByNameWithTypeArgs()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("Dictionary", "System.Collections.Generic.Dictionary", typeof(string), typeof(int));
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(Dictionary)"), typeof(Dictionary<string, int>));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Evaluate()
        {
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate("e * pi"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Evaluate_Array()
        {
            // ReSharper disable ImplicitlyCapturedClosure

            var lengths = new[] { 3, 5, 7 };
            var formatParams = string.Join(", ", Enumerable.Range(0, lengths.Length).Select(position => "{" + position + "}"));

            var hosts = Array.CreateInstance(typeof(object), lengths);
            hosts.Iterate(indices => hosts.SetValue(new HostFunctions(), indices));
            engine.AddHostObject("hostArray", hosts);

            engine.Execute(MiscHelpers.FormatInvariant("dim hosts(" + formatParams + ")", lengths.Select(length => (object)(length - 1)).ToArray()));
            hosts.Iterate(indices => engine.Execute(MiscHelpers.FormatInvariant("set hosts(" + formatParams + ") = hostArray.GetValue(" + formatParams + ")", indices.Select(index => (object)index).ToArray())));
            hosts.Iterate(indices => Assert.AreSame(hosts.GetValue(indices), engine.Evaluate(MiscHelpers.FormatInvariant("hosts(" + formatParams + ")", indices.Select(index => (object)index).ToArray()))));

            var result = engine.Evaluate("hosts");
            Assert.IsInstanceOfType(result, typeof(object[,,]));
            var hostArray = (object[,,])result;
            hosts.Iterate(indices => Assert.AreSame(hosts.GetValue(indices), hostArray.GetValue(indices)));

            // ReSharper restore ImplicitlyCapturedClosure
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Evaluate_WithDocumentName()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(documentName, "e * pi"));
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Evaluate_DiscardDocument()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(documentName, true, "e * pi"));
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Evaluate_RetainDocument()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(documentName, false, "e * pi"));
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Execute()
        {
            engine.Execute("epi = e * pi");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Execute_WithDocumentName()
        {
            const string documentName = "DoTheMath";
            engine.Execute(documentName, "epi = e * pi");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Execute_DiscardDocument()
        {
            const string documentName = "DoTheMath";
            engine.Execute(documentName, true, "epi = e * pi");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Execute_RetainDocument()
        {
            const string documentName = "DoTheMath";
            engine.Execute(documentName, false, "epi = e * pi");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ExecuteCommand_EngineConvert()
        {
            Assert.AreEqual("[ScriptObject:EngineInternalImpl]", engine.ExecuteCommand("eval EngineInternal"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ExecuteCommand_HostConvert()
        {
            var dateHostItem = HostItem.Wrap(engine, new DateTime(2007, 5, 22, 6, 15, 43));
            engine.AddHostObject("date", dateHostItem);
            Assert.AreEqual(dateHostItem.ToString(), engine.ExecuteCommand("eval date"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ExecuteCommand_HostVariable()
        {
            engine.Script.host = new HostFunctions();
            Assert.AreEqual("[HostVariable:String]", engine.ExecuteCommand("eval host.newVar(\"foo\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Invoke_ScriptFunction()
        {
            engine.Execute("function foo(x) : foo = x * pi : end function");
            Assert.AreEqual(Math.E * Math.PI, engine.Invoke("foo", Math.E));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Invoke_HostDelegate()
        {
            engine.Script.foo = new Func<double, double>(x => x * Math.PI);
            Assert.AreEqual(Math.E * Math.PI, engine.Invoke("foo", Math.E));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Interrupt()
        {
            var checkpoint = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(state =>
            {
                checkpoint.WaitOne();
                engine.Interrupt();
            });

            engine.AddHostObject("checkpoint", checkpoint);
            TestUtil.AssertException<OperationCanceledException>(() => engine.Execute("call checkpoint.Set() : while true : foo = \"hello\" : wend"));
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate("e * pi"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        [ExpectedException(typeof(ScriptEngineException))]
        public void VBScriptEngine_AccessContext_Default()
        {
            engine.AddHostObject("test", this);
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AccessContext_Private()
        {
            engine.AddHostObject("test", this);
            engine.AccessContext = GetType();
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ContinuationCallback()
        {
            engine.ContinuationCallback = () => false;
            TestUtil.AssertException<OperationCanceledException>(() => engine.Execute("while true : foo = \"hello\" : wend"));
            engine.ContinuationCallback = null;
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate("e * pi"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_FileNameExtension()
        {
            Assert.AreEqual("vbs", engine.FileNameExtension);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable()
        {
            var host = new HostFunctions();
            engine.Script.host = host;
            Assert.AreSame(host, engine.Script.host);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_Scalar()
        {
            const int value = 123;
            engine.Script.value = value;
            Assert.AreEqual(value, engine.Script.value);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_Enum()
        {
            const DayOfWeek value = DayOfWeek.Wednesday;
            engine.Script.value = value;
            Assert.AreEqual(value, engine.Script.value);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Array()
        {
            // ReSharper disable ImplicitlyCapturedClosure

            var lengths = new[] { 3, 5, 7 };
            var formatParams = string.Join(", ", Enumerable.Range(0, lengths.Length).Select(position => "{" + position + "}"));

            var hosts = Array.CreateInstance(typeof(object), lengths);
            hosts.Iterate(indices => hosts.SetValue(new HostFunctions(), indices));
            engine.Script.hostArray = hosts;

            engine.Execute(MiscHelpers.FormatInvariant("dim hosts(" + formatParams + ")", lengths.Select(length => (object)(length - 1)).ToArray()));
            hosts.Iterate(indices => engine.Execute(MiscHelpers.FormatInvariant("set hosts(" + formatParams + ") = hostArray.GetValue(" + formatParams + ")", indices.Select(index => (object)index).ToArray())));
            hosts.Iterate(indices => Assert.AreSame(hosts.GetValue(indices), engine.Script.hosts.GetValue(indices)));

            var result = engine.Script.hosts;
            Assert.IsInstanceOfType(result, typeof(object[,,]));
            var hostArray = (object[,,])result;
            hosts.Iterate(indices => Assert.AreSame(hosts.GetValue(indices), hostArray.GetValue(indices)));

            // ReSharper restore ImplicitlyCapturedClosure
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_Struct()
        {
            var stamp = new DateTime(2007, 5, 22, 6, 15, 43);
            engine.Script.stamp = stamp;
            Assert.AreEqual(stamp, engine.Script.stamp);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Function()
        {
            engine.Execute("function test(x, y) : test = x * y : end function");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.test(Math.E, Math.PI));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Sub()
        {
            var callbackInvoked = false;
            Action callback = () => callbackInvoked = true;
            engine.Execute("sub test(x) : call x() : end sub");
            engine.Script.test(callback);
            Assert.IsTrue(callbackInvoked);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine
                    Dim host As New HostFunctions
                    engine.Script.host = host
                    Assert.AreSame(host, engine.Script.host)
                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_Scalar_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine
                    Dim value = 123
                    engine.Script.value = value
                    Assert.AreEqual(value, engine.Script.value)
                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_Enum_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine
                    Dim value = DayOfWeek.Wednesday
                    engine.Script.value = value
                    Assert.AreEqual(value, engine.Script.value)
                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Array_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine

                    Dim lengths As Integer() = { 3, 5, 7 }
                    Dim formatParams = String.Join("", "", Enumerable.Range(0, lengths.Length).Select(Function(position) ""{"" & position & ""}""))

                    Dim hosts = Array.CreateInstance(GetType(Object), lengths)
                    TestUtil.Iterate(hosts, Sub(indices) hosts.SetValue(New HostFunctions, indices))
                    engine.Script.hostArray = hosts

                    engine.Execute(TestUtil.FormatInvariant(""dim hosts("" & formatParams & "")"", lengths.Select(Function(length) CType(length - 1, Object)).ToArray()))
                    TestUtil.Iterate(hosts, Sub(indices) engine.Execute(TestUtil.FormatInvariant(""set hosts("" & formatParams & "") = hostArray.GetValue("" & formatParams & "")"", indices.Select(Function(index) CType(index, Object)).ToArray())))
                    TestUtil.Iterate(hosts, Sub(indices) Assert.AreSame(hosts.GetValue(indices), engine.Script.hosts.GetValue(indices)))

                    Dim result = engine.Script.hosts
                    Assert.IsInstanceOfType(result, GetType(Object(,,)))
                    Dim hostArray As Object(,,) = result
                    TestUtil.Iterate(hosts, Sub(indices) Assert.AreSame(hosts.GetValue(indices), hostArray.GetValue(indices)))

                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Variable_Struct_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine
                    Dim stamp = New DateTime(2007, 5, 22, 6, 15, 43)
                    engine.Script.stamp = stamp
                    Assert.AreEqual(stamp, engine.Script.stamp)
                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Function_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine
                    engine.Execute(""function test(x, y) : test = x * y : end function"")
                    Assert.AreEqual(Math.E * Math.PI, engine.Script.test(Math.E, Math.PI))
                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Script_Sub_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New VBScriptEngine
                    Dim callbackInvoked = False
                    Dim callback = Sub() callbackInvoked = True
                    engine.Execute(""sub test(x) : call x() : end sub"")
                    engine.Script.test(callback)
                    Assert.IsTrue(callbackInvoked)
                End Using
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_CollectGarbage()
        {
            // VBScript doesn't support GC
            engine.CollectGarbage(true);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_General()
        {
            using (var console = new StringWriter())
            {
                var clr = new HostTypeCollection(type => type != typeof(Console), "mscorlib", "System", "System.Core");
                clr.GetNamespaceNode("System").SetPropertyNoCheck("Console", console);

                engine.AddHostObject("host", new ExtendedHostFunctions());
                engine.AddHostObject("clr", clr);

                engine.Execute(generalScript);
                Assert.AreEqual(MiscHelpers.FormatCode(generalScriptOutput), console.ToString().Replace("\r\n", "\n"));
            }
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ErrorHandling_ScriptError()
        {
            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("foo = {}; foo();");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNull(exception.InnerException);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ErrorHandling_HostException()
        {
            engine.AddHostObject("host", new HostFunctions());

            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Evaluate("host.proc(0)");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNotNull(exception.InnerException);

                    var hostException = exception.InnerException;
                    Assert.IsInstanceOfType(hostException, typeof(RuntimeBinderException));
                    TestUtil.AssertValidException(hostException);
                    Assert.IsNull(hostException.InnerException);

                    Assert.AreEqual(hostException.Message, exception.Message);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ErrorHandling_IgnoredHostException()
        {
            engine.AddHostObject("host", new HostFunctions());

            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("on error resume next : host.newObj(null) : on error goto 0 : foo = \"foo\" : foo()");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNull(exception.InnerException);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ErrorHandling_NestedScriptError()
        {
            var innerEngine = new JScriptEngine("inner", WindowsScriptEngineFlags.EnableDebugging);
            engine.AddHostObject("engine", innerEngine);

            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("engine.Execute(\"foo = {}; foo();\")");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNotNull(exception.InnerException);

                    var hostException = exception.InnerException;
                    Assert.IsInstanceOfType(hostException, typeof(TargetInvocationException));
                    TestUtil.AssertValidException(hostException);
                    Assert.IsNotNull(hostException.InnerException);

                    var nestedException = hostException.InnerException as ScriptEngineException;
                    Assert.IsNotNull(nestedException);
                    TestUtil.AssertValidException(innerEngine, nestedException);
                    // ReSharper disable once PossibleNullReferenceException
                    Assert.IsNull(nestedException.InnerException);

                    Assert.AreEqual(hostException.Message, exception.Message);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ErrorHandling_NestedHostException()
        {
            var innerEngine = new JScriptEngine("inner", WindowsScriptEngineFlags.EnableDebugging);
            innerEngine.AddHostObject("host", new HostFunctions());
            engine.AddHostObject("engine", innerEngine);

            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("engine.Evaluate(\"host.proc(0)\")");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNotNull(exception.InnerException);

                    var hostException = exception.InnerException;
                    Assert.IsInstanceOfType(hostException, typeof(TargetInvocationException));
                    TestUtil.AssertValidException(hostException);
                    Assert.IsNotNull(hostException.InnerException);

                    var nestedException = hostException.InnerException as ScriptEngineException;
                    Assert.IsNotNull(nestedException);
                    TestUtil.AssertValidException(innerEngine, nestedException);
                    // ReSharper disable once PossibleNullReferenceException
                    Assert.IsNotNull(nestedException.InnerException);

                    var nestedHostException = nestedException.InnerException;
                    Assert.IsInstanceOfType(nestedHostException, typeof(RuntimeBinderException));
                    TestUtil.AssertValidException(nestedHostException);
                    Assert.IsNull(nestedHostException.InnerException);

                    Assert.AreEqual(nestedHostException.Message, nestedException.Message);
                    Assert.AreEqual(hostException.Message, exception.Message);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_CreateInstance()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.AreEqual("foo bar baz qux", engine.Evaluate("host.newObj(testObject, \"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_CreateInstance_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("host.newObj(testObject)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Invoke()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo,bar,baz,qux", engine.Evaluate("testObject(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Invoke_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("testObject()"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo-bar-baz-qux", engine.Evaluate("testObject.DynamicMethod(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.DynamicMethod()"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_FieldOverride()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo.bar.baz.qux", engine.Evaluate("testObject.SomeField(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_FieldOverride_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.SomeField()"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_PropertyOverride()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo:bar:baz:qux", engine.Evaluate("testObject.SomeProperty(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_PropertyOverride_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.SomeProperty()"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_DynamicOverload()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo;bar;baz;qux", engine.Evaluate("testObject.SomeMethod(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_NonDynamicOverload()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual(Math.PI, engine.Evaluate("testObject.SomeMethod()"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_InvokeMethod_NonDynamic()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("Super Bass-O-Matic '76", engine.Evaluate("testObject.ToString()"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_StaticType_Field()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.SomeField"), typeof(HostMethod));
            Assert.AreEqual(12345, engine.Evaluate("host.toStaticType(testObject).SomeField"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_StaticType_Property()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.SomeProperty"), typeof(HostMethod));
            Assert.AreEqual("Bogus", engine.Evaluate("host.toStaticType(testObject).SomeProperty"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_StaticType_Method()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.AreEqual("bar+baz+qux", engine.Evaluate("host.toStaticType(testObject).SomeMethod(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Property()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getProperty(testObject, \"foo\")"), typeof(Undefined));
            Assert.AreEqual(123, engine.Evaluate("host.setProperty(testObject, \"foo\", clng(123))"));
            Assert.AreEqual(123, engine.Evaluate("testObject.foo"));
            Assert.IsTrue((bool)engine.Evaluate("host.removeProperty(testObject, \"foo\")"));
            Assert.IsInstanceOfType(engine.Evaluate("host.getProperty(testObject, \"foo\")"), typeof(Undefined));
            Assert.IsFalse((bool)engine.Evaluate("host.removeProperty(testObject, \"foo\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Property_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getProperty(testObject, \"Zfoo\")"), typeof(Undefined));
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("host.setProperty(testObject, \"Zfoo\", clng(123))"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Property_Invoke()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            engine.Execute("function secret(x) : secret = len(x) : end function");
            Assert.IsInstanceOfType(engine.Evaluate("host.getProperty(testObject, \"foo\")"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.setProperty(testObject, \"foo\", GetRef(\"secret\"))"), typeof(DynamicObject));
            Assert.AreEqual("floccinaucinihilipilification".Length, engine.Evaluate("testObject.foo(\"floccinaucinihilipilification\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Property_Invoke_Nested()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getProperty(testObject, \"foo\")"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.setProperty(testObject, \"foo\", testObject)"), typeof(DynamicObject));
            Assert.AreEqual("foo,bar,baz,qux", engine.Evaluate("testObject.foo(\"foo\", \"bar\", \"baz\", \"qux\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Element()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, clng(1), clng(2), clng(3), \"foo\")"), typeof(Undefined));
            Assert.AreEqual("bar", engine.Evaluate("host.setElement(testObject, \"bar\", clng(1), clng(2), clng(3), \"foo\")"));
            Assert.AreEqual("bar", engine.Evaluate("host.getElement(testObject, clng(1), clng(2), clng(3), \"foo\")"));
            Assert.IsTrue((bool)engine.Evaluate("host.removeElement(testObject, clng(1), clng(2), clng(3), \"foo\")"));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, clng(1), clng(2), clng(3), \"foo\")"), typeof(Undefined));
            Assert.IsFalse((bool)engine.Evaluate("host.removeElement(testObject, clng(1), clng(2), clng(3), \"foo\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Element_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, clng(1), clng(2), clng(3), pi)"), typeof(Undefined));
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("host.setElement(testObject, \"bar\", clng(1), clng(2), clng(3), pi)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_DynamicHostObject_Element_Convert()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            engine.AddHostType("int_t", typeof(int));
            engine.AddHostType("string_t", typeof(string));
            Assert.AreEqual(98765, engine.Evaluate("host.cast(int_t, testObject)"));
            Assert.AreEqual("Booyakasha!", engine.Evaluate("host.cast(string_t, testObject)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_HostIndexers()
        {
            engine.Script.testObject = new TestObject();

            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item(clng(123))"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item.get(clng(123))"));
            engine.Execute("testObject.Item(clng(123)) = pi");
            Assert.AreEqual(Math.PI, engine.Evaluate("testObject.Item(clng(123))"));
            Assert.AreEqual(Math.E, engine.Evaluate("testObject.Item.set(clng(123), e)"));
            Assert.AreEqual(Math.E, engine.Evaluate("testObject.Item.get(clng(123))"));

            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item(\"456\")"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item.get(\"456\")"));
            engine.Execute("testObject.Item(\"456\") = sqr(2)");
            Assert.AreEqual(Math.Sqrt(2), engine.Evaluate("testObject.Item(\"456\")"));
            Assert.AreEqual(Math.Sqrt(3), engine.Evaluate("testObject.Item.set(\"456\", sqr(3))"));
            Assert.AreEqual(Math.Sqrt(3), engine.Evaluate("testObject.Item.get(\"456\")"));

            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item(clng(123), \"456\", 789.987, -0.12345)"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item.get(clng(123), \"456\", 789.987, -0.12345)"));
            engine.Execute("testObject.Item(clng(123), \"456\", 789.987, -0.12345) = sqr(5)");
            Assert.AreEqual(Math.Sqrt(5), engine.Evaluate("testObject.Item(clng(123), \"456\", 789.987, -0.12345)"));
            Assert.AreEqual(Math.Sqrt(7), engine.Evaluate("testObject.Item.set(clng(123), \"456\", 789.987, -0.12345, sqr(7))"));
            Assert.AreEqual(Math.Sqrt(7), engine.Evaluate("testObject.Item.get(clng(123), \"456\", 789.987, -0.12345)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_FormatCode()
        {
            try
            {
                engine.Execute("a", "\n\n\n     x = 3.a");
            }
            catch (ScriptEngineException exception)
            {
                Assert.IsTrue(exception.ErrorDetails.Contains("(a:3:11)"));
            }

            engine.FormatCode = true;
            try
            {
                engine.Execute("b", "\n\n\n     x = 3.a");
            }
            catch (ScriptEngineException exception)
            {
                Assert.IsTrue(exception.ErrorDetails.Contains("(b:0:6)"));
            }
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_GetStackTrace()
        {
            engine.AddHostObject("qux", new Func<object>(() => engine.GetStackTrace()));
            engine.Execute(@"
                function baz():baz = qux():end function
                function bar():bar = baz():end function
                function foo():foo = bar():end function
            ");

            Assert.AreEqual("    at baz (Script Document [3]:1:31) -> baz = qux()\n    at bar (Script Document [3]:2:31) -> bar = baz()\n    at foo (Script Document [3]:3:31) -> foo = bar()\n    at VBScript global code (Script Document [4] [temp]:0:0) -> foo()", engine.Evaluate("foo()"));
            Assert.AreEqual("    at baz (Script Document [3]:1:31) -> baz = qux()\n    at bar (Script Document [3]:2:31) -> bar = baz()\n    at foo (Script Document [3]:3:31) -> foo = bar()", engine.Script.foo());
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_StandardsMode()
        {
            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.EnableStandardsMode);

            // Standards Mode shouldn't affect VBScriptEngine at all
            engine.Execute("function pi : pi = 4 * atn(1) : end function");
            engine.Execute("function e : e = exp(1) : end function");
            VBScriptEngine_Execute();
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_MarshalNullAsDispatch()
        {
            engine.Script.func = new Func<object>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));

            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalNullAsDispatch);

            engine.Script.func = new Func<object>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<string>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<bool?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<char?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<sbyte?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<byte?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<short?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<ushort?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<int?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<uint?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<long?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<ulong?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<float?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<double?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<decimal?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("IsNull(func())"));
            engine.Script.func = new Func<Random>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() is nothing"));
            engine.Script.func = new Func<DayOfWeek?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() is nothing"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_MarshalDecimalAsCurrency()
        {
            // ReSharper disable AccessToDisposedClosure

            engine.Script.func = new Func<object>(() => 123.456M);
            TestUtil.AssertException<ScriptEngineException>(() => engine.Evaluate("TypeName(func())"));

            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalDecimalAsCurrency);

            engine.Script.func = new Func<object>(() => 123.456M);
            Assert.AreEqual("Currency", engine.Evaluate("TypeName(func())"));
            Assert.AreEqual(123.456M + 5, engine.Evaluate("func() + 5"));

            // ReSharper restore AccessToDisposedClosure
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ForEach()
        {
            var collection = new object[] { new HostFunctions(), new Random() };
            engine.Script.collection = collection;
            engine.Execute(forEachTestScript);
            var result = (object[])engine.Evaluate("enumerate(collection)");
            Assert.IsNotNull(result);
            Assert.IsTrue(collection.SequenceEqual(result));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ForEach_Scalar()
        {
            var collection = new[] { 123.456, 789.012 };
            engine.Script.collection = collection;
            engine.Execute(forEachTestScript);
            var result = (object[])engine.Evaluate("enumerate(collection)");
            Assert.IsNotNull(result);
            Assert.IsTrue(collection.SequenceEqual(result.Cast<double>()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ForEach_Enum()
        {
            var collection = new[] { DayOfWeek.Wednesday, DayOfWeek.Saturday };
            engine.Script.collection = collection;
            engine.Execute(forEachTestScript);
            var result = (object[])engine.Evaluate("enumerate(collection)");
            Assert.IsNotNull(result);
            Assert.IsTrue(collection.SequenceEqual(result.Cast<DayOfWeek>()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ForEach_Struct()
        {
            var collection = new[] { DateTime.Now, new DateTime(1941, 8, 26, 11, 35, 20) };
            engine.Script.collection = collection;
            engine.Execute(forEachTestScript);
            var result = (object[])engine.Evaluate("enumerate(collection)");
            Assert.IsNotNull(result);
            Assert.IsTrue(collection.SequenceEqual(result.Cast<DateTime>()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback()
        {
            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                a = ""foo""
                b = 123.456
                test.Method a, b
                call Assert.AreEqual(""foobar"", a)
                call Assert.AreEqual(123.456 * pi, b)
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback_Generic()
        {
            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.AddHostType("String", typeof(string));
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                a = ""foo""
                b = ""bar""
                test.GenericMethod String, a, b
                call Assert.AreEqual(""bar"", a)
                call Assert.AreEqual(""foo"", b)
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback_Static()
        {
            engine.UseReflectionBindFallback = true;
            engine.AddHostType("Test", typeof(ReflectionBindFallbackTest));
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                a = ""foo""
                b = 123.456
                Test.StaticMethod a, b
                call Assert.AreEqual(""foobaz"", a)
                call Assert.AreEqual(123.456 * e, b)
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback_Extension()
        {
            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.AddHostType("ReflectionBindFallbackTestExtensions", typeof(ReflectionBindFallbackTestExtensions));
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                a = ""foo""
                b = 123.456
                test.ExtensionMethod a, b
                call Assert.AreEqual(""foobar"", a)
                call Assert.AreEqual(123.456 * pi, b)
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback_GenericExtension()
        {
            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.AddHostType("ReflectionBindFallbackTestExtensions", typeof(ReflectionBindFallbackTestExtensions));
            engine.AddHostType("String", typeof(string));
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                a = ""foo""
                b = ""bar""
                test.GenericExtensionMethod String, a, b
                call Assert.AreEqual(""bar"", a)
                call Assert.AreEqual(""foo"", b)
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback_HostMarshal()
        {
            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                dim self
                test.GetSelf self
                call Assert.AreEqual(""qux"", self.Property)
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_ReflectionBindFallback_MarshalArrayByValue()
        {
            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.Script.ArrayT = typeof(Array);
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                dim foo
                test.GetArray foo
                call Assert.IsFalse(IsArray(foo))
                call Assert.AreEqual(""abc"", foo.GetValue(0))
                call Assert.AreEqual(123.456, foo.GetValue(1))
            ");

            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalArraysByValue);

            engine.UseReflectionBindFallback = true;
            engine.Script.test = new ReflectionBindFallbackTest();
            engine.AddHostType("Assert", typeof(Assert));
            engine.Execute(@"
                dim foo
                test.GetArray foo
                call Assert.IsTrue(IsArray(foo))
                call Assert.AreEqual(""abc"", foo(0))
                call Assert.AreEqual(123.456, foo(1))
            ");
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_MarshalArraysByValue()
        {
            var foo = new[] { DayOfWeek.Saturday, DayOfWeek.Friday, DayOfWeek.Thursday };

            engine.Script.foo = foo;
            Assert.IsFalse((bool)engine.Evaluate("IsArray(foo)"));

            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalArraysByValue);

            engine.Script.foo = foo;
            Assert.IsTrue((bool)engine.Evaluate("IsArray(foo)"));
            Assert.AreEqual(foo.GetUpperBound(0), engine.Evaluate("UBound(foo, 1)"));

            for (var index = 0; index < foo.Length; index++)
            {
                Assert.AreEqual(foo[index], engine.Evaluate("foo(" + index + ")"));
            }
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_MarshalArraysByValue_CircularReference()
        {
            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalArraysByValue);

            var host = new HostFunctions();
            var foo = new object[] { DayOfWeek.Saturday, DayOfWeek.Friday, new object[] { "abc", 123.456, new object[] { host, null } } };
            ((object[])((object[])foo[2])[2])[1] = foo;

            engine.Script.foo = foo;
            Assert.IsTrue((bool)engine.Evaluate("IsArray(foo)"));
            Assert.AreEqual(foo[0], engine.Evaluate("foo(0)"));
            Assert.AreEqual(foo[1], engine.Evaluate("foo(1)"));
            Assert.IsTrue((bool)engine.Evaluate("IsArray(foo(2))"));
            Assert.AreEqual(((object[])foo[2])[0], engine.Evaluate("foo(2)(0)"));
            Assert.AreEqual(((object[])foo[2])[1], engine.Evaluate("foo(2)(1)"));
            Assert.IsTrue((bool)engine.Evaluate("IsArray(foo(2)(2))"));
            Assert.AreSame(((object[])((object[])foo[2])[2])[0], engine.Evaluate("foo(2)(2)(0)"));

            // circular array reference should have been broken
            Assert.AreSame(((object[])((object[])foo[2])[2])[1], foo);
            Assert.IsNull(engine.Evaluate("foo(2)(2)(1)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_MarshalArraysByValue_Multidimensional()
        {
            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalArraysByValue);

            var foo = new string[4, 3, 2];
            foo.Iterate(indices => foo.SetValue((string.Join(",", indices) + " " + (indices[0] * 256 + indices[1] * 16 + indices[2])), indices));

            engine.Script.foo = foo;
            Assert.IsTrue((bool)engine.Evaluate("IsArray(foo)"));

            for (var dimension = 0; dimension < foo.Rank; dimension++)
            {
                Assert.AreEqual(foo.GetUpperBound(dimension), engine.Evaluate("UBound(foo, " + (dimension + 1) + ")"));
            }

            foo.Iterate(indices => Assert.AreEqual(foo.GetValue(indices), engine.Evaluate("foo(" + string.Join(",", indices) + ")")));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_MarshalArraysByValue_invokeMethod()
        {
            var args = new[] { Math.PI, Math.E };

            engine.Script.args = args;
            engine.Execute("function foo(a, b) : foo = a * b : end function");
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("EngineInternal.invokeMethod(null, GetRef(\"foo\"), args)"));

            engine.Dispose();
            engine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging | WindowsScriptEngineFlags.MarshalArraysByValue);

            engine.Script.args = args;
            engine.Execute("function foo(a, b) : foo = a * b : end function");
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("EngineInternal.invokeMethod(null,  GetRef(\"foo\"), args)"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_COMObject_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                fso = host.newComObj(""Scripting.FileSystemObject"")
                drives = fso.Drives
                en = drives.GetEnumerator()
                while en.MoveNext()
                    list.Add(en.Current.Path)
                wend
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_COMObject_FileSystemObject_ForEach()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                fso = host.newComObj(""Scripting.FileSystemObject"")
                drives = fso.Drives
                for each drive in drives
                    list.Add(drive.Path)
                next
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_COMObject_Dictionary()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.Execute(@"
                dict = host.newComObj(""Scripting.Dictionary"")
                call dict.Add(""foo"", pi)
                call dict.Add(""bar"", e)
                call dict.Add(""baz"", ""abc"")
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Item.set(""foo"", ""pushkin"")
                call dict.Item.set(""bar"", ""gogol"")
                call dict.Item.set(""baz"", pi * e)
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                dict.Item(""foo"") = 987.654
                dict.Item(""bar"") = 321
                dict.Item(""baz"") = ""halloween""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Key.set(""foo"", ""qux"")
                call dict.Key.set(""bar"", pi)
                call dict.Key.set(""baz"", e)
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"qux\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"qux\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(pi)")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(pi)")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(e)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(e)"));

            engine.Execute(@"
                dict.Key(""qux"") = ""foo""
                dict.Key(pi) = ""bar""
                dict.Key(e) = ""baz""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_COMType_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                FSOT = host.comType(""Scripting.FileSystemObject"")
                fso = host.newObj(FSOT)
                drives = fso.Drives
                en = drives.GetEnumerator()
                while en.MoveNext()
                    list.Add(en.Current.Path)
                wend
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_COMType_FileSystemObject_ForEach()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                FSOT = host.comType(""Scripting.FileSystemObject"")
                fso = host.newObj(FSOT)
                drives = fso.Drives
                for each drive in drives
                    list.Add(drive.Path)
                next
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_COMType_Dictionary()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.Execute(@"
                DictT = host.comType(""Scripting.Dictionary"")
                dict = host.newObj(DictT)
                call dict.Add(""foo"", pi)
                call dict.Add(""bar"", e)
                call dict.Add(""baz"", ""abc"")
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Item.set(""foo"", ""pushkin"")
                call dict.Item.set(""bar"", ""gogol"")
                call dict.Item.set(""baz"", pi * e)
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                dict.Item(""foo"") = 987.654
                dict.Item(""bar"") = 321
                dict.Item(""baz"") = ""halloween""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Key.set(""foo"", ""qux"")
                call dict.Key.set(""bar"", pi)
                call dict.Key.set(""baz"", e)
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"qux\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"qux\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(pi)")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(pi)")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(e)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(e)"));

            engine.Execute(@"
                dict.Key(""qux"") = ""foo""
                dict.Key(pi) = ""bar""
                dict.Key(e) = ""baz""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddCOMObject_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.list = list;
            engine.AddCOMObject("fso", "Scripting.FileSystemObject");
            engine.Execute(@"
                drives = fso.Drives
                en = drives.GetEnumerator()
                while en.MoveNext()
                    list.Add(en.Current.Path)
                wend
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddCOMObject_Dictionary()
        {
            engine.AddCOMObject("dict", new Guid("{ee09b103-97e0-11cf-978f-00a02463e06f}"));
            engine.Execute(@"
                call dict.Add(""foo"", pi)
                call dict.Add(""bar"", e)
                call dict.Add(""baz"", ""abc"")
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Item.set(""foo"", ""pushkin"")
                call dict.Item.set(""bar"", ""gogol"")
                call dict.Item.set(""baz"", pi * e)
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                dict.Item(""foo"") = 987.654
                dict.Item(""bar"") = 321
                dict.Item(""baz"") = ""halloween""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Key.set(""foo"", ""qux"")
                call dict.Key.set(""bar"", pi)
                call dict.Key.set(""baz"", e)
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"qux\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"qux\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(pi)")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(pi)")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(e)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(e)"));

            engine.Execute(@"
                dict.Key(""qux"") = ""foo""
                dict.Key(pi) = ""bar""
                dict.Key(e) = ""baz""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddCOMType_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.host = new HostFunctions();
            engine.Script.list = list;
            engine.AddCOMType("FSOT", "Scripting.FileSystemObject");
            engine.Execute(@"
                fso = host.newObj(FSOT)
                drives = fso.Drives
                en = drives.GetEnumerator()
                while en.MoveNext()
                    list.Add(en.Current.Path)
                wend
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddCOMType_Dictionary()
        {
            engine.Script.host = new HostFunctions();
            engine.AddCOMType("DictT", new Guid("{ee09b103-97e0-11cf-978f-00a02463e06f}"));
            engine.Execute(@"
                dict = host.newObj(DictT)
                call dict.Add(""foo"", pi)
                call dict.Add(""bar"", e)
                call dict.Add(""baz"", ""abc"")
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Item.set(""foo"", ""pushkin"")
                call dict.Item.set(""bar"", ""gogol"")
                call dict.Item.set(""baz"", pi * e)
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item(\"bar\")"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get(\"bar\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                dict.Item(""foo"") = 987.654
                dict.Item(""bar"") = 321
                dict.Item(""baz"") = ""halloween""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));

            engine.Execute(@"
                call dict.Key.set(""foo"", ""qux"")
                call dict.Key.set(""bar"", pi)
                call dict.Key.set(""baz"", e)
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"qux\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"qux\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(pi)")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(pi)")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(e)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(e)"));

            engine.Execute(@"
                dict.Key(""qux"") = ""foo""
                dict.Key(pi) = ""bar""
                dict.Key(e) = ""baz""
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item(\"foo\")"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get(\"foo\")"));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item(\"bar\")")));
            Assert.AreEqual(321, Convert.ToInt32(engine.Evaluate("dict.Item.get(\"bar\")")));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(\"baz\")"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(\"baz\")"));
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_AddCOMType_XMLHTTP()
        {
            int status = 0;
            string data = null;

            var thread = new Thread(() =>
            {
                using (var testEngine = new VBScriptEngine(WindowsScriptEngineFlags.EnableDebugging))
                {
                    using (var helperEngine = new JScriptEngine(WindowsScriptEngineFlags.EnableStandardsMode))
                    {
                        // ReSharper disable AccessToDisposedClosure

                        testEngine.Script.onComplete = new Action<int, string>((xhrStatus, xhrData) =>
                        {
                            status = xhrStatus;
                            data = xhrData;
                            Dispatcher.ExitAllFrames();
                        });

                        testEngine.Script.getData = new Func<string, string>(responseText =>
                            helperEngine.Script.JSON.parse(responseText).data
                        );

                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                        {
                            testEngine.AddCOMType("XMLHttpRequest", "MSXML2.XMLHTTP");
                            testEngine.Script.host = new HostFunctions();
                            testEngine.Execute(@"
                                sub onreadystatechange
                                    if xhr.readyState = 4 then
                                        call onComplete(xhr.status, getData(xhr.responseText))
                                    end if
                                end sub
                                xhr = host.newObj(XMLHttpRequest)
                                call xhr.open(""POST"", ""http://httpbin.org/post"", true)
                                xhr.onreadystatechange = GetRef(""onreadystatechange"")
                                call xhr.send(""Hello, world!"")
                            ");
                        }));

                        Dispatcher.Run();

                        // ReSharper restore AccessToDisposedClosure
                    }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.AreEqual(200, status);
            Assert.AreEqual("Hello, world!", data);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_EnableAutoHostVariables()
        {
            const string pre = "123";
            var value = "foo";
            const int post = 456;

            engine.Execute("function foo(a, x, b) : dim y : y = x : x = a & \"bar\" & b: foo = y : end function");
            Assert.AreEqual("foo", engine.Script.foo(pre, ref value, post));
            Assert.AreEqual("123bar456", value);

            value = "foo";
            engine.EnableAutoHostVariables = true;
            engine.Execute("function foo(a, x, b) : dim y : y = x.value : x.value = a & \"bar\" & b : foo = y : end function");
            Assert.AreEqual("foo", engine.Script.foo(pre, ref value, post));
            Assert.AreEqual("123bar456", value);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_EnableAutoHostVariables_Delegate()
        {
            const string pre = "123";
            var value = "foo";
            const int post = 456;

            engine.Execute("function foo(a, x, b) : dim y : y = x : x = a & \"bar\" & b : foo = y : end function");
            var del = DelegateFactory.CreateDelegate<TestDelegate>(engine, engine.Evaluate("GetRef(\"foo\")"));
            Assert.AreEqual("foo", del(pre, ref value, post));
            Assert.AreEqual("123bar456", value);

            value = "foo";
            engine.EnableAutoHostVariables = true;
            engine.Execute("function foo(a, x, b) : dim y : y = x.value : x.value = a & \"bar\" & b : foo = y : end function");
            del = DelegateFactory.CreateDelegate<TestDelegate>(engine, engine.Evaluate("GetRef(\"foo\")"));
            Assert.AreEqual("foo", del(pre, ref value, post));
            Assert.AreEqual("123bar456", value);
        }

        [TestMethod, TestCategory("VBScriptEngine")]
        public void VBScriptEngine_Current()
        {
            // ReSharper disable AccessToDisposedClosure

            using (var innerEngine = new VBScriptEngine())
            {
                engine.Script.test = new Action(() =>
                {
                    innerEngine.Script.test = new Action(() => Assert.AreSame(innerEngine, ScriptEngine.Current));
                    Assert.AreSame(engine, ScriptEngine.Current);
                    innerEngine.Execute("test()");
                    innerEngine.Script.test();
                    Assert.AreSame(engine, ScriptEngine.Current);
                });

                Assert.IsNull(ScriptEngine.Current);
                engine.Execute("test()");
                engine.Script.test();
                Assert.IsNull(ScriptEngine.Current);
            }

            // ReSharper restore AccessToDisposedClosure
        }

        // ReSharper restore InconsistentNaming

        #endregion

        #region miscellaneous

        public class ReflectionBindFallbackTest
        {
            public string Property { get { return "qux"; } }

            public void Method(ref string a, ref double b)
            {
                a = a + "bar";
                b = b * Math.PI;
            }

            public void GenericMethod<T>(ref T a, ref T b)
            {
                var temp = a;
                a = b;
                b = temp;
            }

            public static void StaticMethod(ref string a, ref double b)
            {
                a = a + "baz";
                b = b * Math.E;
            }

            public void GetSelf(out object self)
            {
                self = this;
            }

            public void GetArray(out object array)
            {
                array = new object[] { "abc", 123.456 };
            }
        }

        private const string generalScript =
        @"
            set System = clr.System

            set TestObjectT = host.type(""Microsoft.ClearScript.Test.GeneralTestObject"", ""ClearScriptTest"")
            set tlist = host.newObj(System.Collections.Generic.List(TestObjectT))
            call tlist.Add(host.newObj(TestObjectT, ""Eóin"", 20))
            call tlist.Add(host.newObj(TestObjectT, ""Shane"", 16))
            call tlist.Add(host.newObj(TestObjectT, ""Cillian"", 8))
            call tlist.Add(host.newObj(TestObjectT, ""Sasha"", 6))
            call tlist.Add(host.newObj(TestObjectT, ""Brian"", 3))

            class VBTestObject
               public name
               public age
            end class

            function createTestObject(name, age)
               dim testObject
               set testObject = new VBTestObject
               testObject.name = name
               testObject.age = age
               set createTestObject = testObject
            end function

            set olist = host.newObj(System.Collections.Generic.List(System.Object))
            call olist.Add(createTestObject(""Brian"", 3))
            call olist.Add(createTestObject(""Sasha"", 6))
            call olist.Add(createTestObject(""Cillian"", 8))
            call olist.Add(createTestObject(""Shane"", 16))
            call olist.Add(createTestObject(""Eóin"", 20))

            set dict = host.newObj(System.Collections.Generic.Dictionary(System.String, System.String))
            call dict.Add(""foo"", ""bar"")
            call dict.Add(""baz"", ""qux"")
            set value = host.newVar(System.String)
            result = dict.TryGetValue(""foo"", value.out)

            set expando = host.newObj(System.Dynamic.ExpandoObject)
            set expandoCollection = host.cast(System.Collections.Generic.ICollection(System.Collections.Generic.KeyValuePair(System.String, System.Object)), expando)

            set onEventRef = GetRef(""onEvent"")
            sub onEvent(s, e)
                call System.Console.WriteLine(""Property changed: {0}; new value: {1}"", e.PropertyName, eval(""s."" + e.PropertyName))
            end sub

            set onStaticEventRef = GetRef(""onStaticEvent"")
            sub onStaticEvent(s, e)
                call System.Console.WriteLine(""Property changed: {0}; new value: {1} (static event)"", e.PropertyName, e.PropertyValue)
            end sub

            set eventCookie = tlist.Item(0).Change.connect(onEventRef)
            set staticEventCookie = TestObjectT.StaticChange.connect(onStaticEventRef)
            tlist.Item(0).Name = ""Jerry""
            tlist.Item(1).Name = ""Ellis""
            tlist.Item(0).Name = ""Eóin""
            tlist.Item(1).Name = ""Shane""

            call eventCookie.disconnect()
            call staticEventCookie.disconnect()
            tlist.Item(0).Name = ""Jerry""
            tlist.Item(1).Name = ""Ellis""
            tlist.Item(0).Name = ""Eóin""
            tlist.Item(1).Name = ""Shane""
        ";

        private const string generalScriptOutput =
        @"
            Property changed: Name; new value: Jerry
            Property changed: Name; new value: Jerry (static event)
            Property changed: Name; new value: Ellis (static event)
            Property changed: Name; new value: Eóin
            Property changed: Name; new value: Eóin (static event)
            Property changed: Name; new value: Shane (static event)
        ";

        private const string forEachTestScript =
        @"
            function enumerate(collection)
                dim index, array()
                index = -1
                for each item in collection
                    index = index + 1
                    redim preserve array(index)
                    array(index) = item
                next
                enumerate = array
            end function
        ";

        public object TestProperty { get; set; }

        public static object StaticTestProperty { get; set; }

        // ReSharper disable UnusedMember.Local

        private void PrivateMethod()
        {
        }

        private static void PrivateStaticMethod()
        {
        }

        // ReSharper restore UnusedMember.Local

        private delegate string TestDelegate(string pre, ref string value, int post);

        #endregion
    }

    public static class ReflectionBindFallbackTestExtensions
    {
        public static void ExtensionMethod(this VBScriptEngineTest.ReflectionBindFallbackTest test, ref string a, ref double b)
        {
            a = a + "bar";
            b = b * Math.PI;
        }

        public static void GenericExtensionMethod<T>(this VBScriptEngineTest.ReflectionBindFallbackTest test, ref T a, ref T b)
        {
            var temp = a;
            a = b;
            b = temp;
        }
    }
}
