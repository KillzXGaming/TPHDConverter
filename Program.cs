using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuperBMDLib;
using SuperBMDLib.Geometry;
using SuperBMDLib.Rigging;
using SuperBMDLib.Geometry.Enums;
using SuperBMDLib.Util;
using System.IO;

namespace GmxConverter
{
    class Program
    {
        static void Main(string[] args) {

            string fileName = args[0];

         //  var ogGmx = new GMX_Parser();
        //   ogGmx.Read(new Toolbox.Core.IO.FileReader(File.OpenRead("al.bmd.gmx")));

            var model = Model.Load(File.OpenRead(fileName));
            var gmx = CreateGMX(model);

            foreach (var mesh in gmx.Meshes)
            {
                Console.WriteLine($"MESH {mesh.VertexCount} {mesh.FaceCount} {mesh.SkinningFlags} {mesh.VertexSize}");
            }

            using (var stream = new FileStream(fileName + ".gmx", FileMode.Create, FileAccess.Write)) {
                gmx.Write(new Toolbox.Core.IO.FileWriter(stream));
            }

         //   ogGmx.Read(new Toolbox.Core.IO.FileReader(File.OpenRead("al.gmx")));
        }

        static GMX_Parser CreateGMX(Model model)
        {
            GMX_Parser gmx = new GMX_Parser();
            foreach (var curShape in model.Shapes.Shapes) {
                gmx.Meshes.Add(CreateMesh(model, curShape));

                int numPackets = curShape.Packets.Count;
                for (int i = 0; i < numPackets - 1; i++)
                    gmx.Meshes.Add(new GMX_Parser.MESH() { SkinningFlags = 20 });

            }

            return gmx;
        }

        static GMX_Parser.MESH CreateMesh(Model model, Shape curShape)
        {
            var attributes = model.VertexData.Attributes;

            var mesh = new GMX_Parser.MESH();
            mesh.IndexGroup = new GMX_Parser.INDX();
            mesh.VertexGroup = new GMX_Parser.VERT();
            mesh.VMapGroup = new GMX_Parser.VMAP();

            List<GMX_Parser.Vertex> vertices = new List<GMX_Parser.Vertex>();
            List<ushort> indices = new List<ushort>();
            List<ushort> boneindices = new List<ushort>();
            List<ushort> vmapindices = new List<ushort>();

            ushort vertexID = 0;
            uint vertexStride = 0;

            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                vertexStride += 4;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Position))
                vertexStride += 12;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Normal))
                vertexStride += 12;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Color0))
                vertexStride += 4;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Color1))
                vertexStride += 4;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Tex0))
                vertexStride += 8;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Tex1))
                vertexStride += 8;

            uint[] matrices = new uint[10];
            uint matrixStart = 0;
            foreach (Packet pack in curShape.Packets)
            {
                for (int i = 0; i < pack.MatrixIndices.Count; i++)
                {
                    if (pack.MatrixIndices[i] != -1)
                        matrices[i] = (uint)pack.MatrixIndices[i];
                }

                foreach (Primitive prim in pack.Primitives)
                {
                    List<Vertex> triVertices = J3DUtility.PrimitiveToTriangles(prim);
                    for (int triIndex = 0; triIndex < triVertices.Count; triIndex += 3)
                    {
                        indices.AddRange(new ushort[] { (ushort)(vertexID + 2), (ushort)(vertexID + 1), vertexID });

                        for (int triVertIndex = 0; triVertIndex < 3; triVertIndex++)
                        {
                            var gmxVertex = new GMX_Parser.Vertex();

                            Vertex vert = triVertices[triIndex + triVertIndex];
                            var position = attributes.Positions[(int)vert.GetAttributeIndex(GXVertexAttribute.Position)];

                            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Normal))
                            {
                                gmxVertex.Normal = attributes.Normals[(int)vert.GetAttributeIndex(GXVertexAttribute.Normal)];
                            }
                            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Color0))
                            {
                                var color = attributes.Color_0[(int)vert.GetAttributeIndex(GXVertexAttribute.Color0)];
                                gmxVertex.Color = new OpenTK.Vector4(color.R, color.G, color.B, color.A);
                            }
                            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Tex0))
                            {
                                gmxVertex.TexCoord0 = attributes.TexCoord_0[(int)vert.GetAttributeIndex(GXVertexAttribute.Tex0)];
                            }
                            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Tex1))
                            {
                                gmxVertex.TexCoord1 = attributes.TexCoord_1[(int)vert.GetAttributeIndex(GXVertexAttribute.Tex1)];
                            }
                            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                            {
                                gmxVertex.MatrixID = (uint)(matrixStart + vert.PositionMatrixIDxIndex);
                            }

                            gmxVertex.Position = position;
                            vertices.Add(gmxVertex);
                        }
                    }
                }
                matrixStart += 10;
            }

            indices.Clear();

            //Now optmize the indices
            Dictionary<GMX_Parser.Vertex, int> verticesNew = new Dictionary<GMX_Parser.Vertex, int>();
            foreach (var v in vertices)
            {
                if (!verticesNew.ContainsKey(v))
                    verticesNew.Add(v, verticesNew.Count);

                if (verticesNew.ContainsKey(v))
                    indices.Add((ushort)verticesNew[v]);
            }

            vertices = verticesNew.Keys.ToList();

            foreach (var v in vertices)
            {
                vmapindices.Add(0);
                vmapindices.Add(0);
            }

            mesh.VMapGroup.Indices = vmapindices.ToArray();
            mesh.VertexSize = (ushort)vertexStride;
            mesh.VertexCount = (ushort)vertices.Count;
            mesh.VertexGroup.Vertices = vertices;
            mesh.FaceCount = (uint)indices.Count;
            mesh.IndexGroup.Indices = indices.ToArray();
            mesh.SkinningFlags = 0;
            if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                mesh.SkinningFlags = 20;
            return mesh;
        }
    }
}
