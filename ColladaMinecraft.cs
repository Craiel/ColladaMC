namespace ColladaMC
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using CarbonCore.Processing.Resource.Model;
    using CarbonCore.Processing.Source.Collada;
    using CarbonCore.Utils.Contracts.IoC;
    using CarbonCore.Utils.Diagnostics;
    using CarbonCore.Utils.IO;
    using CarbonCore.UtilsCommandLine.Contracts;

    using ColladaMC.Contracts;

    using Cyotek.Data.Nbt;

    using SharpDX;

    internal class Block
    {
        private readonly List<string> materials;

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------
        public Block()
        {
            this.materials = new List<string>();
        }

        // -------------------------------------------------------------------
        // Public
        // -------------------------------------------------------------------
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public IList<string> Materials
        {
            get
            {
                return this.materials;
            }
        }

        public override int GetHashCode()
        {
            return Tuple.Create(this.X, this.Y, this.Z).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var typed = obj as Block;
            if (typed == null)
            {
                return false;
            }

            return typed.X == this.X && typed.Y == this.Y && typed.Z == this.Z;
        }
    }

    public class ColladaMinecraft : IColladaMinecraft
    {
        private const int MaxHeight = 240;

        private readonly IFactory factory;

        private readonly ICommandLineArguments arguments;
        private readonly IDictionary<Block, Block> blocks;

        private CarbonFile sourceFile;

        private ColladaInfo modelInfo;
        private ModelResourceGroup model;
        private BoundingBox modelBoundingBox;
        private BoundingBox outputBoundingBox;
        private Vector3 boundingBoxSize;
        private Vector3 zeroOffset;
        private float scaleFactor = 10.0f;

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------
        public ColladaMinecraft(IFactory factory)
        {
            this.factory = factory;

            this.arguments = factory.Resolve<ICommandLineArguments>();
            this.RegisterCommandLineArguments();

            this.modelBoundingBox = new BoundingBox(new Vector3(0), new Vector3(0));

            this.blocks = new Dictionary<Block, Block>();
        }

        // -------------------------------------------------------------------
        // Public
        // -------------------------------------------------------------------
        public void Process()
        {
            if (!this.arguments.ParseCommandLineArguments())
            {
                this.arguments.PrintArgumentUse();
                return;
            }

            this.DoProcess();
        }

        // -------------------------------------------------------------------
        // Private
        // -------------------------------------------------------------------
        private void RegisterCommandLineArguments()
        {
            ICommandLineSwitchDefinition definition = this.arguments.Define("s", "sourceFile", x => this.sourceFile = new CarbonFile(x));
            definition.Required = true;
            definition.RequireArgument = true;
            definition.Description = "The source file to process";
        }

        private void DoProcess()
        {
            if (this.sourceFile == null || !this.sourceFile.Exists)
            {
                this.arguments.PrintArgumentUse();
                return;
            }

            CarbonFile targetFile = this.sourceFile.ChangeExtension(".schematic");

            var reader = new BinaryTagReader();
            TagCompound test1;
            if (targetFile.Exists)
            {
                test1 = reader.Load(targetFile.GetPath());
            }

            var test2 = reader.Load(@"C:\Games\Minecraft\ColladaMC\Untitled World.schematic");

            using (new ProfileRegion("Load Model Info"))
            {
                this.modelInfo = new ColladaInfo(this.sourceFile);
                System.Diagnostics.Trace.TraceInformation("Model has {0} meshes", this.modelInfo.MeshInfos.Count);
            }

            using (new ProfileRegion("Process collada"))
            {
                this.model = ColladaProcessor.Process(this.modelInfo, null, null);
            }

            using (new ProfileRegion("Updating bounding box"))
            {
                this.UpdateBoundingBox();
            }

            using (new ProfileRegion("Converting mesh"))
            {
                this.ConvertMesh();
            }

            var sizeVector = this.outputBoundingBox.Maximum - this.outputBoundingBox.Minimum + new Vector3(1);
            int maxAddress = (int)(sizeVector.Y * sizeVector.Z * sizeVector.X);
            using (var file = File.Open(targetFile.GetPath(), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var writer = new BinaryTagWriter(file);
                var compound = new TagCompound("Schematic");
                compound.Value.Add(new TagShort("Height", (short)sizeVector.Y));
                compound.Value.Add(new TagShort("Length", (short)sizeVector.Z));
                compound.Value.Add(new TagShort("Width", (short)sizeVector.X));
                compound.Value.Add(new TagList("Entities"));
                compound.Value.Add(new TagList("TileEntities"));
                compound.Value.Add(new TagString("Materials", "Alpha"));
                
                var blockArray = new byte[maxAddress];
                var biomeArray = new byte[(int)(sizeVector.Z * sizeVector.X)];
                var dataArray = new byte[maxAddress];

                foreach (Block block in this.blocks.Keys)
                {
                    // (Y×length + Z)×width + X
                    int address = this.GetInternalAddress(sizeVector, new Vector3(block.X, block.Y, block.Z));
                    blockArray[address] = 41; // Gold block for testing
                    dataArray[address] = 0;
                }

                /*int count = 0;
                for (var x = 0; x < sizeVector.X; x++)
                {
                    for (var y = 0; y < sizeVector.Y; y++)
                    {
                        for (var z = 0; z < sizeVector.Z; z++)
                        {
                            int test = this.GetInternalAddress(sizeVector, new Vector3(x, y, z));
                            blockArray[test] = 23;
                            count++;
                        }
                    }
                }*/

                /*for (int i = 0; i < sizeVector.Y; i++)
                {
                    //blockArray[i] = 1;
                    int test = this.GetInternalAddress(sizeVector, new Vector3(i, i, i));
                    blockArray[test] = 1;
                }*/

                compound.Value.Add(new TagByteArray("Data", dataArray));
                compound.Value.Add(new TagByteArray("Biomes", biomeArray));
                compound.Value.Add(new TagByteArray("Blocks", blockArray));
                writer.Write(compound);
            }
        }

        private int GetInternalAddress(Vector3 sizeVector, Vector3 address)
        {
            //Vector3 internalVector = address / sizeVector;
            return (int)((address.Y * (sizeVector.Z * sizeVector.X)) + (address.Z * sizeVector.X) + address.X);
            //return (int)((address.Y*sizeVector.Z+address.Z)*sizeVector.X+address.X);
            //return (int)(address.X + (address.Y * sizeVector.Y + address.Z) * sizeVector.X); //x + (y * Height + z) * Width
            //return (int)((address.Y * sizeVector.Z + address.Z) * sizeVector.X + address.X);
            //return (int)((((internalVector.Y * (int)sizeVector.Z) + internalVector.Z) * (int)sizeVector.X) + internalVector.X);
        }

        private void UpdateBoundingBox()
        {
            foreach (ModelResourceGroup @group in model.Groups)
            {
                foreach (ModelResource modelResource in @group.Models)
                {
                    if (modelResource.BoundingBox == null)
                    {
                        modelResource.CalculateBoundingBox();
                    }

                    this.modelBoundingBox = BoundingBox.Merge(
                        this.modelBoundingBox, modelResource.BoundingBox.Value);
                }
            }

            this.zeroOffset = new Vector3(0) - this.modelBoundingBox.Minimum;
            this.boundingBoxSize = this.modelBoundingBox.Maximum - this.modelBoundingBox.Minimum;
            if (this.boundingBoxSize.Y > MaxHeight)
            {
                this.scaleFactor = MaxHeight / this.boundingBoxSize.Y;
            }
        }

        private void ConvertMesh()
        {
            System.Diagnostics.Trace.TraceInformation("Converting mesh: ");
            foreach (ModelResourceGroup @group in model.Groups)
            {
                System.Diagnostics.Trace.TraceInformation("  # Group {0}", @group.Name);
                foreach (ModelResource modelResource in @group.Models)
                {
                    System.Diagnostics.Trace.TraceInformation("    - {0}", modelResource.Name);
                    foreach (ModelResourceElement element in modelResource.Elements)
                    {
                        this.AddBlock(modelResource.Name, element);
                    }
                }
            }
        }
        
        private void AddBlock(string modelName, ModelResourceElement element)
        {
            Vector3 zeroBasedPosition = (element.Position + this.zeroOffset) * this.scaleFactor;
            var block = new Block
                            {
                                X = (int)Math.Round(zeroBasedPosition.X),
                                Y = (int)Math.Round(zeroBasedPosition.Y),
                                Z = (int)Math.Round(zeroBasedPosition.Z)
                            };

            if (this.blocks.ContainsKey(block))
            {
                var existing = this.blocks[block];
                if (existing.Materials.Contains(modelName))
                {
                    return;
                }

                this.blocks[block].Materials.Add(modelName);
            }
            else
            {
                block.Materials.Add(modelName);
                this.blocks.Add(block, block);

                var pos = new Vector3(block.X, block.Y, block.Z);
                this.outputBoundingBox = BoundingBox.Merge(this.outputBoundingBox, new BoundingBox(pos, pos));
            }
        }
    }
}
