using System;
using System.Diagnostics;
using System.IO;
using Tommy;

namespace Ccxc.Core.Utils
{
    public class SystemOption
    {
        private const string DefaultConfigPath = "Config/ccxc.config.toml";
        /// <summary>
        /// 从单个配置文件xml中解析配置文件。
        /// </summary>
        /// <typeparam name="T">Option实体类</typeparam>
        /// <param name="configPath">配置文件路径</param>
        /// <returns></returns>
        public static T GetOption<T>(string sectionName) where T : class, new()
        {
            var configDirectory = Path.GetDirectoryName(DefaultConfigPath) ?? "Config";
            if (!Directory.Exists(configDirectory))
            {
                Logger.Info("Config目录不存在，建立该目录");
                Directory.CreateDirectory(configDirectory);
            }

            //读取配置文件
            var configPath = DefaultConfigPath;
            if (sectionName.EndsWith(".xml"))
            {
                sectionName = sectionName[..^4]; //Config格式修改后兼容原代码
            }
            return GetSingleConfig<T>(configPath, sectionName);
        }

        private static T GetSingleConfig<T>(string configPath, string sectionName) where T : class, new()
        {
            TomlTable tomlTable = null;

            //判断配置文件是否存在
            if (!File.Exists(configPath))
            {
                Console.WriteLine("配置文件未找到，将写入默认配置文件");
                var config = new T();
                tomlTable = new TomlTable();
                UpgradeConfigFile<T>(tomlTable, configPath, sectionName);
                return config;
            }

            //读取配置文件
            using (var fs = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var sr = new StreamReader(fs);
                tomlTable = TOML.Parse(sr);
            }

            UpgradeConfigFile<T>(tomlTable, configPath, sectionName);

            //解析配置文件并填入config对象
            var baseConfig = new T();
            var baseDirectory = Path.GetDirectoryName(configPath);
            if (tomlTable["extends"] is TomlArray extendArray)
            {
                foreach (var extend in extendArray)
                {
                    if (extend is TomlString extendString)
                    {
                        var extendPath = Path.Combine(baseDirectory, extendString.Value);
                        if (File.Exists(extendPath))
                        {
                            using (var fs = new FileStream(extendPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using var sr = new StreamReader(fs);
                                //var ftext = sr.ReadToEndAsync().GetAwaiter().GetResult();
                                var extendTable = TOML.Parse(sr);
                                ConfigMerge(baseConfig, extendTable, sectionName);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"继承的配置文件 {extendPath} 不存在");
                        }
                    }
                }
            }
            ConfigMerge(baseConfig, tomlTable, sectionName);
            if (tomlTable["mixin"] is TomlArray mixinArray)
            {
                foreach (var mixin in mixinArray)
                {
                    if (mixin is TomlString mixinString)
                    {
                        var mixinPath = Path.Combine(baseDirectory, mixinString.Value);
                        if (File.Exists(mixinPath))
                        {
                            using (var fs = new FileStream(mixinPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using var sr = new StreamReader(fs);
                                var mixinTable = TOML.Parse(sr);
                                ConfigMerge(baseConfig, mixinTable, sectionName);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"混入的配置文件 {mixinPath} 不存在");
                        }
                    }
                }
            }

            return baseConfig;
        }

        private static void ConfigMerge<T>(T baseConfig, TomlTable extendTable, string sectionName) where T : class, new()
        {
            if (extendTable[sectionName] is TomlTable configTable)
            {
                foreach (var props in typeof(T).GetProperties())
                {
                    var info = Attribute.GetCustomAttributes(props);
                    foreach (var attr in info)
                    {
                        if (attr is OptionDescriptionAttribute oattr)
                        {
                            var name = props.Name;
                            if (configTable.HasKey(name))
                            {
                                switch (props.PropertyType.Name)
                                {
                                    case "Int32":
                                        if (configTable[name] is TomlInteger i32)
                                        {
                                            if (i32.Value != default) props.SetValue(baseConfig, (int)i32.Value);
                                        }
                                        else if (configTable[name] is TomlString si32)
                                        {
                                            if (int.TryParse(si32.Value, out var i32s))
                                            {
                                                if (i32s != default) props.SetValue(baseConfig, i32s);
                                            }
                                        }
                                        break;
                                    case "Int64":
                                        if (configTable[name] is TomlInteger i64)
                                        {
                                            if (i64.Value != default) props.SetValue(baseConfig, i64.Value);
                                        }
                                        else if (configTable[name] is TomlString si64)
                                        {
                                            if (long.TryParse(si64.Value, out var i64s))
                                            {
                                                if (i64s != default) props.SetValue(baseConfig, i64s);
                                            }
                                        }
                                        break;
                                    case "Double":
                                        if (configTable[name] is TomlFloat dbl)
                                        {
                                            if (dbl.Value != default) props.SetValue(baseConfig, dbl.Value);
                                        }
                                        else if (configTable[name] is TomlString sdbl)
                                        {
                                            if (double.TryParse(sdbl.Value, out var dbls))
                                            {
                                                if (dbls != default) props.SetValue(baseConfig, dbls);
                                            }
                                        }
                                        break;
                                    case "Boolean":
                                        if (configTable[name] is TomlBoolean bl)
                                        {
                                            props.SetValue(baseConfig, bl.Value);
                                        }
                                        else if (configTable[name] is TomlString sbl)
                                        {
                                            if (sbl.Value == "true" || sbl.Value == "True")
                                            {
                                                props.SetValue(baseConfig, true);
                                            }
                                            else if (sbl.Value == "false" || sbl.Value == "False")
                                            {
                                                props.SetValue(baseConfig, false);
                                            }
                                            else if (bool.TryParse(sbl.Value, out var bls))
                                            {
                                                props.SetValue(baseConfig, bls);
                                            }
                                        }
                                        break;
                                    case "DateTime":
                                        if (configTable[name] is TomlDateTimeLocal dt)
                                        {
                                            if (dt.Value != default) props.SetValue(baseConfig, dt.Value);
                                        }
                                        else if (configTable[name] is TomlString sdt)
                                        {
                                            if (DateTime.TryParse(sdt.Value, out var dts))
                                            {
                                                if (dts != default) props.SetValue(baseConfig, dts);
                                            }
                                        }
                                        break;
                                    default:
                                        if (configTable[name] is TomlString str)
                                        {
                                            if (!string.IsNullOrEmpty(str.Value)) props.SetValue(baseConfig, str.Value);
                                        }
                                        else
                                        {
                                            var valueString = configTable[name].AsString.Value;
                                            if (!string.IsNullOrEmpty(valueString)) props.SetValue(baseConfig, valueString);
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void UpgradeConfigFile<T>(TomlTable tomlTable, string configPath, string sectionName) where T : class, new()
        {
            var writeFlag = false; // 是否需要回写配置文件
            //Meta段落： 检查配置文件中是否存在extend和mixin配置项
            if (!tomlTable.HasKey("extends"))
            {
                tomlTable["extends"] = new TomlArray
                {
                    Comment = "继承的配置文件。本配置文件中未定义的配置项将从继承的配置文件中继承。"
                };
                writeFlag = true;
            }
            if (!tomlTable.HasKey("mixin"))
            {
                tomlTable["mixin"] = new TomlArray
                {
                    Comment = "混入的配置文件。覆盖本配置文件中的同名配置项。"
                };
                writeFlag = true;
            }

            //检查配置文件中是否存在此段落
            if (!tomlTable.HasKey(sectionName))
            {
                tomlTable[sectionName] = new TomlTable();
                writeFlag = true;
            }

            //检查配置段落中是否已经存在所有配置项，若不存在，添加一个空白的配置项。
            var configTable = tomlTable[sectionName] as TomlTable;
            foreach (var props in typeof(T).GetProperties())
            {
                var info = Attribute.GetCustomAttributes(props);
                foreach (var attr in info)
                {
                    if (attr is OptionDescriptionAttribute)
                    {
                        var name = props.Name;
                        var comment = (attr as OptionDescriptionAttribute).Desc;

                        if (!configTable.HasKey(name))
                        {
                            configTable[name] = new TomlString
                            {
                                Comment = comment,
                                Value = string.Empty
                            };
                            writeFlag = true;
                        }
                    }
                }
            }

            //回写配置文件
            if (writeFlag)
            {
                using var fs = new FileStream(configPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs);
                tomlTable.WriteTo(sw);
                sw.Flush();
            }
        }

        public static void WriteConfigFileGenerate<T>(string sectionName, string name, string value) where T : class, new()
        {
            //读取配置文件
            var configPath = DefaultConfigPath;
            var tomlTable = new TomlTable();
            using (var fs = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var sr = new StreamReader(fs);
                tomlTable = TOML.Parse(sr);
            }

            if (!tomlTable.HasKey(sectionName))
            {
                tomlTable[sectionName] = new TomlTable();
            }

            var configTable = tomlTable[sectionName] as TomlTable;
            if (!configTable.HasKey(name))
            {
                configTable[name] = new TomlString
                {
                    Value = value,
                };
            }
            else
            {
                configTable[name] = value;
            }

            //回写配置文件
            using var fs2 = new FileStream(configPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs2);
            tomlTable.WriteTo(sw);
            sw.Flush();
        }


    }

    /// <summary>
    /// 标注该属性为一个系统配置项
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class OptionDescriptionAttribute : Attribute
    {
        /// <summary>
        /// 该系统配置项的描述
        /// </summary>
        public string Desc { get; set; }

        /// <summary>
        /// 标注该属性为一个系统配置项
        /// </summary>
        /// <param name="value">该配置项的描述</param>
        public OptionDescriptionAttribute(string value)
        {
            Desc = value;
        }
    }
}
