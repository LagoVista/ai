// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 64b688f0f86204310868ff5bbeebc1fb5eb6139a4924695003b8379e46260a98
// IndexVersion: 2
// --- END CODE INDEX META ---
using Microsoft.ML.OnnxRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NUnit.Framework;


namespace LagoVista.AI.Tests
{
    [TestClass]
    [Category("IntegrationTests")]
    public class ModelTests
    {
        [TestMethod]
        public void GetShapeTest()
        {
            var session = new InferenceSession(@".\resnet50-v2-7.onnx");
            var metadataa = session.ModelMetadata;
        }
    }
}
