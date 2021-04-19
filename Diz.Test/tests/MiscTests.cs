using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Diz.Core.export;
using Diz.Core.export.assemblyGenerators;
using Diz.Core.util;
using LightInject;
using Xunit;

namespace Diz.Test.tests
{
    public interface ILogCreator
    {
        
    }
    
    public interface IAsm
    {
        
    }

    public class AsmGeneratorTypeAttribute : Attribute
    {
        public string Tag { get; private set; }
        public AsmGeneratorTypeAttribute(string tag)
        {
            Tag = tag;
        }   
    }
    
 
    
    [AsmGeneratorType("%map")]
    public class Asm1 : IAsm
    {
        public ILogCreator LogCreator { get; set; }
    }

    public class FakeLogCreator : ILogCreator
    {
        
    }
    
    public static class MiscTests
    {
        [Fact]
        public static void TestServiceContainer()
        {
            ServiceProvider.Register(
                typeof(AssemblyPartialLineGenerator).Assembly, 
                typeof(IAssemblyPartialGenerator));

            var container = ServiceProvider.ServiceContainer;
            container.Register<ILogCreator, FakeLogCreator>();
            var tag = typeof(Asm1).GetCustomAttribute<AsmGeneratorTypeAttribute>()?.Tag;
            if (tag != null)
                container.Register<IAsm, Asm1>(tag);
                
            var foo = (Asm1)container.GetInstance<IAsm>("%map");
            Assert.NotNull(foo.LogCreator);

            Assert.True(ServiceProvider.ServiceContainer.AvailableServices
                .FirstOrDefault(s =>
                s.ImplementingType == typeof(AssemblyGenerateComment)) != null);
            
            
        }
        
        [Fact]
        public static void TestHex1()
        {
            Assert.Equal(0xF, ByteUtil.ByteParseHex1('F'));
        }

        private const string ValidHexChars = "0123456789ABCDEF";
        
        [Fact]
        public static void TestHexRange()
        {
            for (var c = '\0'; c < 255; ++c)
            {
                if (ValidHexChars.Contains(char.ToUpper(c)))
                {
                    Assert.Equal(Convert.ToInt32(c.ToString(), 16), ByteUtil.ByteParseHex1(c));
                }
                else
                {
                    Assert.Throws<InvalidDataException>(() => ByteUtil.ByteParseHex1(c));    
                }
                
            }
        }

        [Fact]
        public static void TestHex2()
        {
            Assert.Equal(0xF0, ByteUtil.ByteParseHex2('F', '0'));
        }

        [Fact]
        public static void TestHex4()
        {
            Assert.Equal((uint)0xF029, ByteUtil.ByteParseHex4('F', '0', '2', '9'));
        }

        [Fact]
        public static void TestHexM()
        {
            Assert.Equal((uint)0xF029, ByteUtil.ByteParseHex("F029", 0, 4));
            Assert.Equal((uint)0xF02, ByteUtil.ByteParseHex("F02", 0, 3));
            Assert.Equal((uint)0xF0, ByteUtil.ByteParseHex("F0", 0, 2));
            Assert.Equal((uint)0xF, ByteUtil.ByteParseHex("F", 0, 1));
            
            Assert.Equal((uint)0xF, ByteUtil.ByteParseHex(" F", 1, 1));
            Assert.Equal((uint)0xF2, ByteUtil.ByteParseHex(" F2 ", 1, 2));
            Assert.Equal((uint)0xF029, ByteUtil.ByteParseHex(" F0297", 1, 4));
            Assert.Equal((uint)0xF02979A, ByteUtil.ByteParseHex(" F02979A", 1, 7));
            Assert.Equal(0xF02979A9, ByteUtil.ByteParseHex(" F02979A9", 1, 8));
            
            Assert.Throws<ArgumentException>(() => ByteUtil.ByteParseHex("F02988554324AC", 0, 9));
            Assert.Throws<ArgumentException>(() => ByteUtil.ByteParseHex("F029", 0, 0));
        }
    }
}