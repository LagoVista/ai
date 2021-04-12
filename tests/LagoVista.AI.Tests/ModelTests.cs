using Microsoft.ML.OnnxRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Tests
{
    [TestClass]
    public class ModelTests
    {
        [TestMethod]
        public void GetShapeTest()
        {
            var session = new InferenceSession(@"s:\ai\resnet50-v2-7.onnx");
            var metadataa = session.ModelMetadata;
        }
    }
}
