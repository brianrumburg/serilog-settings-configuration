﻿using System;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Settings.Configuration.Tests.Support;
using TestDummies;
using TestDummies.Console;
using TestDummies.Console.Themes;
using Xunit;

namespace Serilog.Settings.Configuration.Tests
{
    public class ConfigurationSettingsTests
    {
        static LoggerConfiguration ConfigFromJson(string jsonString, string secondJsonSource = null)
        {
            var builder = new ConfigurationBuilder().AddJsonString(jsonString);
            if (secondJsonSource != null)
                builder.AddJsonString(secondJsonSource);
            var config = builder.Build();
            return new LoggerConfiguration()
                .ReadFrom.Configuration(config);
        }

        [Fact]
        public void PropertyEnrichmentIsApplied()
        {
            LogEvent evt = null;

            var json = @"{
                ""Serilog"": {            
                    ""Properties"": {
                        ""App"": ""Test""
                    }
                }
            }";

            var log = ConfigFromJson(json)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Information("Has a test property");

            Assert.NotNull(evt);
            Assert.Equal("Test", evt.Properties["App"].LiteralValue());
        }

        [Theory]
        [InlineData("extended syntax",
            @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [
                        { ""Name"": ""DummyConsole""},
                        { ""Name"": ""DummyWithLevelSwitch""},
                    ]        
                }
            }")]
        [InlineData("simplified syntax",
            @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [""DummyConsole"", ""DummyWithLevelSwitch"" ]        
                }
            }")]
        public void ParameterlessSinksAreConfigured(string syntax, string json)
        {
            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyConsoleSink.Emitted.Clear();
            DummyWithLevelSwitchSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(1, DummyConsoleSink.Emitted.Count);
            Assert.Equal(1, DummyWithLevelSwitchSink.Emitted.Count);
        }

        [Fact]
        public void SinksAreConfigured()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\""}
                    }]        
                }
            }";

            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();
            DummyRollingFileAuditSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
            Assert.Equal(0, DummyRollingFileAuditSink.Emitted.Count);
        }

        [Fact]
        public void AuditSinksAreConfigured()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""AuditTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\""}
                    }]        
                }
            }";

            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();
            DummyRollingFileAuditSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(0, DummyRollingFileSink.Emitted.Count);
            Assert.Equal(1, DummyRollingFileAuditSink.Emitted.Count);
        }

        [Fact]
        public void TestMinimumLevelOverrides()
        {
            var json = @"{
                ""Serilog"": {
                    ""MinimumLevel"" : {
                        ""Override"" : {
                            ""System"" : ""Warning""
                        }
                    }        
                }
            }";

            LogEvent evt = null;

            var log = ConfigFromJson(json)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            var systemLogger = log.ForContext<WeakReference>();
            systemLogger.Write(Some.InformationEvent());

            Assert.Null(evt);

            systemLogger.Warning("Bad things");
            Assert.NotNull(evt);

            evt = null;
            log.Write(Some.InformationEvent());
            Assert.NotNull(evt);
        }

        [Fact]
        public void SinksWithAbstractParamsAreConfiguredWithTypeName()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyConsole"",
                        ""Args"": {""theme"" : ""Serilog.Settings.Configuration.Tests.Support.CustomConsoleTheme, Serilog.Settings.Configuration.Tests""}
                    }]        
                }
            }";

            DummyConsoleSink.Theme = null;

            ConfigFromJson(json)
                .CreateLogger();

            Assert.NotNull(DummyConsoleSink.Theme);
            Assert.IsType<CustomConsoleTheme>(DummyConsoleSink.Theme);
        }

        [Fact]
        public void SinksAreConfiguredWithStaticMember()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyConsole"",
                        ""Args"": {""theme"" : ""TestDummies.Console.Themes.ConsoleThemes::Theme1, TestDummies""}
                    }]        
                }
            }";

            DummyConsoleSink.Theme = null;

            ConfigFromJson(json)
                .CreateLogger();

            Assert.Equal(ConsoleThemes.Theme1, DummyConsoleSink.Theme);
        }

        [Theory]
        [InlineData("$switchName", true)]
        [InlineData("$SwitchName", true)]
        [InlineData("$switch1", true)]
        [InlineData("$sw1tch0", true)]
        [InlineData("$SWITCHNAME", true)]
        [InlineData("$$switchname", false)]
        [InlineData("$switchname$", false)]
        [InlineData("switch$name", false)]
        [InlineData("$", false)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData("$1switch", false)]
        [InlineData("$switch_name", false)]
        public void LoggingLevelSwitchNameValidityScenarios(string switchName, bool expectedValid)
        {
            Assert.True(ConfigurationReader.IsValidSwitchName(switchName) == expectedValid,
                $"expected IsValidSwitchName({switchName}) to return {expectedValid} ");
        }

        [Fact]
        public void LoggingLevelSwitchWithInvalidNameThrowsFormatException()
        {
            var json = @"{
                ""Serilog"": {            
                    ""LevelSwitches"": {""switchNameNotStartingWithDollar"" : ""Warning"" }
                }
            }";

            var ex = Assert.Throws<FormatException>(() => ConfigFromJson(json));

            Assert.Contains("\"switchNameNotStartingWithDollar\"", ex.Message);
            Assert.Contains("'$' sign", ex.Message);
            Assert.Contains("\"LevelSwitches\" : {\"$switchName\" :", ex.Message);
        }

        [Fact]
        public void LoggingLevelSwitchIsConfigured()
        {
            var json = @"{
                ""Serilog"": {            
                    ""LevelSwitches"": {""$switch1"" : ""Warning"" },
                    ""MinimumLevel"" : {
                        ""ControlledBy"" : ""$switch1""
                    }
                }
            }";
            LogEvent evt = null;

            var log = ConfigFromJson(json)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.DebugEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning. It should not log Debug messages");
            log.Write(Some.InformationEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning. It should not log Information messages");
            log.Write(Some.WarningEvent());
            Assert.True(evt != null, "LoggingLevelSwitch initial level was Warning. It should log Warning messages");
        }

        [Fact]
        public void SettingMinimumLevelControlledByToAnUndeclaredSwitchThrows()
        {
            var json = @"{
                ""Serilog"": {            
                    ""LevelSwitches"": {""$switch1"" : ""Warning"" },
                    ""MinimumLevel"" : {
                        ""ControlledBy"" : ""$switch2""
                    }
                }
            }";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ConfigFromJson(json)
                    .CreateLogger());

            Assert.Contains("$switch2", ex.Message);
            Assert.Contains("\"LevelSwitches\":{\"$switch2\":", ex.Message);
        }

        [Fact]
        public void LoggingLevelSwitchIsPassedToSinks()
        {
            var json = @"{
                ""Serilog"": {      
                    ""Using"": [""TestDummies""],
                    ""LevelSwitches"": {""$switch1"" : ""Information"" },
                    ""MinimumLevel"" : {
                        ""ControlledBy"" : ""$switch1""
                    },
                    ""WriteTo"": [{
                        ""Name"": ""DummyWithLevelSwitch"",
                        ""Args"": {""controlLevelSwitch"" : ""$switch1""}
                    }]      
                }
            }";

            LogEvent evt = null;

            var log = ConfigFromJson(json)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            Assert.False(DummyWithLevelSwitchSink.ControlLevelSwitch == null, "Sink ControlLevelSwitch should have been initialized");

            var controlSwitch = DummyWithLevelSwitchSink.ControlLevelSwitch;
            Assert.NotNull(controlSwitch);

            log.Write(Some.DebugEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was information. It should not log Debug messages");

            controlSwitch.MinimumLevel = LogEventLevel.Debug;
            log.Write(Some.DebugEvent());
            Assert.True(evt != null, "LoggingLevelSwitch level was changed to Debug. It should log Debug messages");
        }

        [Fact]
        public void ReferencingAnUndeclaredSwitchInSinkThrows()
        {
            var json = @"{
                ""Serilog"": {      
                    ""Using"": [""TestDummies""],
                    ""LevelSwitches"": {""$switch1"" : ""Information"" },
                    ""MinimumLevel"" : {
                        ""ControlledBy"" : ""$switch1""
                    },
                    ""WriteTo"": [{
                        ""Name"": ""DummyWithLevelSwitch"",
                        ""Args"": {""controlLevelSwitch"" : ""$switch2""}
                    }]      
                }
            }";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ConfigFromJson(json)
                    .CreateLogger());

            Assert.Contains("$switch2", ex.Message);
            Assert.Contains("\"LevelSwitches\":{\"$switch2\":", ex.Message);
        }

        [Fact]
        public void LoggingLevelSwitchCanBeUsedForMinimumLevelOverrides()
        {
            var json = @"{
                ""Serilog"": {
                    ""Using"": [""TestDummies""],
                    ""LevelSwitches"": {""$specificSwitch"" : ""Warning"" },
                    ""MinimumLevel"" : {
                        ""Default"" : ""Debug"",
                        ""Override"" : {
                            ""System"" : ""$specificSwitch""
                        }
                    },
                    ""WriteTo"": [{
                        ""Name"": ""DummyWithLevelSwitch"",
                        ""Args"": {""controlLevelSwitch"" : ""$specificSwitch""}
                    }]     
                }
            }";

            LogEvent evt = null;

            var log = ConfigFromJson(json)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            var systemLogger = log.ForContext(Constants.SourceContextPropertyName, "System.Bar");

            log.Write(Some.InformationEvent());
            Assert.False(evt is null, "Minimum level is Debug. It should log Information messages");

            evt = null;
            // ReSharper disable HeuristicUnreachableCode
            systemLogger.Write(Some.InformationEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning for logger System.*. It should not log Information messages for SourceContext System.Bar");

            systemLogger.Write(Some.WarningEvent());
            Assert.False(evt is null, "LoggingLevelSwitch initial level was Warning for logger System.*. It should log Warning messages for SourceContext System.Bar");


            evt = null;
            var controlSwitch = DummyWithLevelSwitchSink.ControlLevelSwitch;

            controlSwitch.MinimumLevel = LogEventLevel.Information;
            systemLogger.Write(Some.InformationEvent());
            Assert.False(evt is null, "LoggingLevelSwitch level was changed to Information for logger System.*. It should now log Information events for SourceContext System.Bar.");
            // ReSharper restore HeuristicUnreachableCode
        }

        [Fact]
        public void SinkWithIConfigurationArguments()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\"",
                                   ""configurationSection"" : { ""foo"" : ""bar"" } }
                    }]        
                }
            }";

            // IConfiguration and IConfigurationSection arguments do not have
            // default values so they will throw if they are not populated

            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
        }

        [Fact]
        public void SinkWithConfigurationBindingArgument()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\"",
                                   ""objectBinding"" : [ { ""foo"" : ""bar"" }, { ""abc"" : ""xyz"" } ] }
                    }]        
                }
            }";

            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
        }

        [Fact]
        public void SinkWithStringArrayArgument()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\"",
                                   ""stringArrayBinding"" : [ ""foo"", ""bar"", ""baz"" ] }
                    }]        
                }
            }";

            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
        }

        [Fact]
        public void SinkWithIntArrayArgument()
        {
            var json = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\"",
                                   ""intArrayBinding"" : [ 1,2,3,4,5 ] }
                    }]        
                }
            }";

            var log = ConfigFromJson(json)
                .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();

            log.Write(Some.InformationEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
        }

        [Trait("Bugfix", "#91")]
        [Fact]
        public void WriteToLoggerWithRestrictedToMinimumLevelIsSupported()
        {
            var json = @"{
            ""Serilog"": {            
                ""Using"": [""TestDummies""],
                ""WriteTo"": [{
                    ""Name"": ""Logger"",
                    ""Args"": {
                        ""configureLogger"" : {
                            ""WriteTo"": [{
                                ""Name"": ""DummyRollingFile"",
                                ""Args"": {""pathFormat"" : ""C:\\""}
                            }]},
                        ""restrictedToMinimumLevel"": ""Warning"" 
                    }
                }]        
            }
            }";

            var log = ConfigFromJson(json)
            .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();

            log.Write(Some.InformationEvent());
            log.Write(Some.WarningEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
        }

        [Trait("Bugfix", "#91")]
        [Fact]
        public void WriteToSubLoggerWithLevelSwitchIsSupported()
        {
            var json = @"{
            ""Serilog"": {            
                ""Using"": [""TestDummies""],
                ""LevelSwitches"": {""$switch1"" : ""Warning"" },          
                ""MinimumLevel"" : {
                        ""ControlledBy"" : ""$switch1""
                    },
                ""WriteTo"": [{
                    ""Name"": ""Logger"",
                    ""Args"": {
                        ""configureLogger"" : {
                            ""WriteTo"": [{
                                ""Name"": ""DummyRollingFile"",
                                ""Args"": {""pathFormat"" : ""C:\\""}
                            }]}
                    }
                }]        
            }
            }";

            var log = ConfigFromJson(json)
            .CreateLogger();

            DummyRollingFileSink.Emitted.Clear();

            log.Write(Some.InformationEvent());
            log.Write(Some.WarningEvent());

            Assert.Equal(1, DummyRollingFileSink.Emitted.Count);
        }

        [Trait("Bugfix", "#103")]
        [Fact]
        public void InconsistentComplexVsScalarArgumentValuesThrowsIOE()
        {
            var jsonDiscreteValue = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : ""C:\\""}
                    }]        
                }
            }";

            var jsonComplexValue = @"{
                ""Serilog"": {            
                    ""Using"": [""TestDummies""],
                    ""WriteTo"": [{
                        ""Name"": ""DummyRollingFile"",
                        ""Args"": {""pathFormat"" : { ""foo"" : ""bar"" } }
                    }]        
                }
            }";

            // These will combine into a ConfigurationSection object that has both
            // Value == "C:\" and GetChildren() == List<string>. No configuration
            // extension matching this exists (in theory an "object" argument could
            // accept either value). ConfigurationReader should throw as soon as
            // the multiple values are recognized; it will never attempt to locate
            // a matching argument.

            var ex = Assert.Throws<InvalidOperationException>(()
                => ConfigFromJson(jsonDiscreteValue, jsonComplexValue));

            Assert.Contains("The value for the argument", ex.Message);
            Assert.Contains("'Serilog:WriteTo:0:Args:pathFormat'", ex.Message);
        }

        [Fact]
        public void DestructureLimitsNestingDepth()
        {
            var json = @"{
                ""Serilog"": {
                    ""Destructure"": [
                    {
                        ""Name"": ""ToMaximumDepth"",
                        ""Args"": { ""maximumDestructuringDepth"": 3 }
                    }]
                }
            }";

            var NestedObject = new
            {
                A = new
                {
                    B = new
                    {
                        C = new
                        {
                            D = "F"
                        }
                    }
                }
            };

            var msg = GetDestructuredProperty(NestedObject, json);

            Assert.Contains("C", msg);
            Assert.DoesNotContain("D", msg);
        }

        [Fact]
        public void DestructureLimitsStringLength()
        {
            var json = @"{
                ""Serilog"": {
                    ""Destructure"": [
                    {
                        ""Name"": ""ToMaximumStringLength"",
                        ""Args"": { ""maximumStringLength"": 3 }
                    }]
                }
            }";

            var inputString = "ABCDEFGH";
            var msg = GetDestructuredProperty(inputString, json);

            Assert.Equal("\"AB…\"", msg);
        }

        [Fact]
        public void DestructureLimitsCollectionCount()
        {
            var json = @"{
                ""Serilog"": {
                    ""Destructure"": [
                    {
                        ""Name"": ""ToMaximumCollectionCount"",
                        ""Args"": { ""maximumCollectionCount"": 3 }
                    }]
                }
            }";

            var collection = new[] { 1, 2, 3, 4, 5, 6 };
            var msg = GetDestructuredProperty(collection, json);

            Assert.Contains("3", msg);
            Assert.DoesNotContain("4", msg);
        }

        private string GetDestructuredProperty(object x, string json)
        {
            LogEvent evt = null;
            var log = ConfigFromJson(json)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();
            log.Information("{@X}", x);
            var result = evt.Properties["X"].ToString();
            return result;
        }
    }
}
