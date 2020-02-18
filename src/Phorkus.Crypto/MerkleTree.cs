using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Crypto
{
    public class MerkleTreeNode
    {
        /// <summary>
        /// Node hash
        /// </summary>
        public UInt256? Hash;

        /// <summary>
        /// Parent node
        /// </summary>
        public MerkleTreeNode? Parent;

        /// <summary>
        /// Left child node
        /// </summary>
        public MerkleTreeNode? LeftChild;

        /// <summary>
        /// Right child node
        /// </summary>
        public MerkleTreeNode? RightChild;

        /// <summary>
        /// Is root
        /// </summary>
        public bool IsRoot => Parent == null;

        /// <summary>
        /// Is leaf
        /// </summary>
        public bool IsLeaf => LeftChild == null && RightChild == null;

        /// <summary>
        /// Constructor
        /// </summary>
        public MerkleTreeNode()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hash">Hash</param>
        public MerkleTreeNode(UInt256 hash)
        {
            Hash = hash;
        }


        /// <summary>
        /// Get leafs form node
        /// </summary>
        /// <param name="node">Node to start</param>
        /// <returns>Enumerate leafs</returns>
        private static IEnumerable<MerkleTreeNode> GetLeafs(MerkleTreeNode node)
        {
            if (node == null)
                yield break;
            if (node.IsLeaf)
                yield return node;
            if (node.LeftChild != null)
            {
                foreach (var a in GetLeafs(node.LeftChild))
                    yield return a;
            }

            if (node.RightChild == null)
                yield break;
            foreach (var a in GetLeafs(node.RightChild))
                yield return a;
        }

        /// <summary>
        /// Get leafs from current node
        /// </summary>
        /// <returns>Enumerate leafs</returns>
        public IEnumerable<MerkleTreeNode> GetLeafs()
        {
            return GetLeafs(this);
        }
    }

    /// <summary>
    /// Merkle Tree
    /// </summary>
    public class MerkleTree
    {
        /// <summary>
        /// Constant hash length
        /// </summary>
        private const int HashSize = 32;

        /// <summary>
        /// Constant double hash length
        /// </summary>
        private const int Hash2Size = 64;

        /// <summary>
        /// Tree Root
        /// </summary>
        public readonly MerkleTreeNode Root;

        /// <summary>
        /// Tree Height
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hashes">Hash Array</param>
        private MerkleTree(IReadOnlyCollection<UInt256> hashes)
        {
            if (hashes.Count == 0)
                throw new ArgumentException();

            Root = Build(hashes.Select(p => new MerkleTreeNode(p)).ToArray());
            var depth = 1;
            for (var i = Root; i.LeftChild != null; i = i.LeftChild)
                depth++;

            Depth = depth;
        }

        /// <summary>
        /// Build a node
        /// </summary>
        /// <param name="leaves">Leaves nodes</param>
        /// <returns>Node</returns>
        private static MerkleTreeNode Build(IReadOnlyList<MerkleTreeNode> leaves)
        {
            if (leaves.Count == 0)
                throw new ArgumentException(nameof(leaves.Count));
            if (leaves.Count == 1)
                return leaves[0];

            var parents = new MerkleTreeNode[(leaves.Count + 1) / 2];

            for (var i = 0; i < parents.Length; i++)
            {
                var current = new MerkleTreeNode
                {
                    LeftChild = leaves[i * 2]
                };

                parents[i] = current;

                leaves[i * 2].Parent = current;

                if (i * 2 + 1 == leaves.Count)
                {
                    current.RightChild = current.LeftChild;
                }
                else
                {
                    current.RightChild = leaves[i * 2 + 1];
                    leaves[i * 2 + 1].Parent = current;
                }

                var hash = new byte[Hash2Size];
                Array.Copy(current.LeftChild.Hash.ToByteArray(), 0, hash, 0, HashSize);
                Array.Copy(current.RightChild.Hash.ToByteArray(), 0, hash, HashSize, HashSize);
                current.Hash = hash.Keccak256().ToUInt256();
            }

            return Build(parents); //TailCall
        }

        /// <summary>
        /// Calculate Root node value
        /// </summary>
        /// <param name="hashes">Hash list</param>
        /// <returns>Result of the calculation</returns>
        public static UInt256? ComputeRoot(IReadOnlyCollection<UInt256> hashes)
        {
            if (hashes == null || hashes.Count == 0)
                return null;
            if (hashes.Count == 1)
                return hashes.First();
            var tree = new MerkleTree(hashes);
            return tree.Root.Hash;
        }

        /// <summary>
        /// Calculate Tree
        /// </summary>
        /// <param name="hashes">Hash list</param>
        /// <returns>Result of the calculation</returns>
        public static MerkleTree ComputeTree(UInt256[] hashes)
        {
            if (hashes == null || hashes.Length == 0)
                throw new ArgumentException(nameof(hashes));

            return new MerkleTree(hashes);
        }

        /// <summary>
        /// List tree node hashes
        /// </summary>
        /// <param name="node">Node</param>
        /// <param name="hashes">List to return hashes</param>
        private static void DepthFirstSearch(MerkleTreeNode node, ICollection<UInt256> hashes)
        {
            if (node.LeftChild is null || node.RightChild is null)
            {
                // if left is null, then right must be null
                if (!(node.RightChild is null && node.LeftChild is null) || node.Hash is null)
                {
                    throw new InvalidOperationException("Corrupted merkle tree");
                }

                hashes.Add(node.Hash);
                return;
            }

            DepthFirstSearch(node.LeftChild, hashes);
            DepthFirstSearch(node.RightChild, hashes);
        }

        /// <summary>
        /// List tree node hashes
        /// </summary>
        /// <returns>Byte array with node hashes</returns>
        public UInt256[] ToHashArray()
        {
            var hashes = new List<UInt256>();
            DepthFirstSearch(Root, hashes);
            return hashes.ToArray();
        }

        /// <summary>
        /// Tree Pruning by bit mask
        /// </summary>
        /// <param name="flags">Flags</param>
        public void Trim(BitArray flags)
        {
            flags = new BitArray(flags)
            {
                Length = 1 << (Depth - 1)
            };
            Trim(Root, 0, Depth, flags);
        }

        /// <summary>
        /// Tree Pruning by bit mask
        /// </summary>
        /// <param name="node">Node</param>
        /// <param name="index">Index</param>
        /// <param name="depth">Depth</param>
        /// <param name="flags">Flags</param>
        private static void Trim(MerkleTreeNode node, int index, int depth, BitArray flags)
        {
            if (depth == 1) return;
            if (node.LeftChild is null || node.RightChild is null) return; // if left is null, then right must be null
            if (depth == 2)
            {
                if (flags.Get(index * 2) || flags.Get(index * 2 + 1))
                    return;
                node.LeftChild = null;
                node.RightChild = null;
                return;
            }

            Trim(node.LeftChild, index * 2, depth - 1, flags);
            Trim(node.RightChild, index * 2 + 1, depth - 1, flags);
            if (!(node.LeftChild.LeftChild is null && node.RightChild.RightChild is null))
                return;
            node.LeftChild = null;
            node.RightChild = null;
        }

        /// <summary>
        /// Search node by hash
        /// </summary>
        /// <param name="hash">Hash</param>
        /// <param name="node">Start node</param>
        /// <returns>Node</returns>
        public MerkleTreeNode? Search(UInt256 hash, MerkleTreeNode? node = null)
        {
            if (node == null) node = Root;
            if (node.Hash is null || node.Hash.Equals(hash)) return node;

            if (node.LeftChild != null)
            {
                var a = Search(hash, node.LeftChild);
                if (a != null) return a;
            }

            if (node.RightChild != null)
            {
                var a = Search(hash, node.RightChild);
                if (a != null) return a;
            }

            return null;
        }
    }
}