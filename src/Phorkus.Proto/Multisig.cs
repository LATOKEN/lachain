// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: multisig.proto
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Phorkus.Proto {

  /// <summary>Holder for reflection information generated from multisig.proto</summary>
  public static partial class MultisigReflection {

    #region Descriptor
    /// <summary>File descriptor for multisig.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static MultisigReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cg5tdWx0aXNpZy5wcm90bxoNZGVmYXVsdC5wcm90byK6AQoITXVsdGlTaWcS",
            "DgoGcXVvcnVtGAEgASgNEh4KCnZhbGlkYXRvcnMYAiADKAsyCi5QdWJsaWNL",
            "ZXkSMgoKc2lnbmF0dXJlcxgDIAMoCzIeLk11bHRpU2lnLlNpZ25hdHVyZUJ5",
            "VmFsaWRhdG9yGkoKFFNpZ25hdHVyZUJ5VmFsaWRhdG9yEhcKA2tleRgBIAEo",
            "CzIKLlB1YmxpY0tleRIZCgV2YWx1ZRgCIAEoCzIKLlNpZ25hdHVyZUIjChFj",
            "b20ubGF0b2tlbi5wcm90b6oCDVBob3JrdXMuUHJvdG9iBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Phorkus.Proto.DefaultReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Phorkus.Proto.MultiSig), global::Phorkus.Proto.MultiSig.Parser, new[]{ "Quorum", "Validators", "Signatures" }, null, null, new pbr::GeneratedClrTypeInfo[] { new pbr::GeneratedClrTypeInfo(typeof(global::Phorkus.Proto.MultiSig.Types.SignatureByValidator), global::Phorkus.Proto.MultiSig.Types.SignatureByValidator.Parser, new[]{ "Key", "Value" }, null, null, null)})
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class MultiSig : pb::IMessage<MultiSig> {
    private static readonly pb::MessageParser<MultiSig> _parser = new pb::MessageParser<MultiSig>(() => new MultiSig());
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<MultiSig> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Phorkus.Proto.MultisigReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public MultiSig() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public MultiSig(MultiSig other) : this() {
      quorum_ = other.quorum_;
      validators_ = other.validators_.Clone();
      signatures_ = other.signatures_.Clone();
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public MultiSig Clone() {
      return new MultiSig(this);
    }

    /// <summary>Field number for the "quorum" field.</summary>
    public const int QuorumFieldNumber = 1;
    private uint quorum_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public uint Quorum {
      get { return quorum_; }
      set {
        quorum_ = value;
      }
    }

    /// <summary>Field number for the "validators" field.</summary>
    public const int ValidatorsFieldNumber = 2;
    private static readonly pb::FieldCodec<global::Phorkus.Proto.PublicKey> _repeated_validators_codec
        = pb::FieldCodec.ForMessage(18, global::Phorkus.Proto.PublicKey.Parser);
    private readonly pbc::RepeatedField<global::Phorkus.Proto.PublicKey> validators_ = new pbc::RepeatedField<global::Phorkus.Proto.PublicKey>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Phorkus.Proto.PublicKey> Validators {
      get { return validators_; }
    }

    /// <summary>Field number for the "signatures" field.</summary>
    public const int SignaturesFieldNumber = 3;
    private static readonly pb::FieldCodec<global::Phorkus.Proto.MultiSig.Types.SignatureByValidator> _repeated_signatures_codec
        = pb::FieldCodec.ForMessage(26, global::Phorkus.Proto.MultiSig.Types.SignatureByValidator.Parser);
    private readonly pbc::RepeatedField<global::Phorkus.Proto.MultiSig.Types.SignatureByValidator> signatures_ = new pbc::RepeatedField<global::Phorkus.Proto.MultiSig.Types.SignatureByValidator>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Phorkus.Proto.MultiSig.Types.SignatureByValidator> Signatures {
      get { return signatures_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as MultiSig);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(MultiSig other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Quorum != other.Quorum) return false;
      if(!validators_.Equals(other.validators_)) return false;
      if(!signatures_.Equals(other.signatures_)) return false;
      return true;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Quorum != 0) hash ^= Quorum.GetHashCode();
      hash ^= validators_.GetHashCode();
      hash ^= signatures_.GetHashCode();
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (Quorum != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(Quorum);
      }
      validators_.WriteTo(output, _repeated_validators_codec);
      signatures_.WriteTo(output, _repeated_signatures_codec);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Quorum != 0) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(Quorum);
      }
      size += validators_.CalculateSize(_repeated_validators_codec);
      size += signatures_.CalculateSize(_repeated_signatures_codec);
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(MultiSig other) {
      if (other == null) {
        return;
      }
      if (other.Quorum != 0) {
        Quorum = other.Quorum;
      }
      validators_.Add(other.validators_);
      signatures_.Add(other.signatures_);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            input.SkipLastField();
            break;
          case 8: {
            Quorum = input.ReadUInt32();
            break;
          }
          case 18: {
            validators_.AddEntriesFrom(input, _repeated_validators_codec);
            break;
          }
          case 26: {
            signatures_.AddEntriesFrom(input, _repeated_signatures_codec);
            break;
          }
        }
      }
    }

    #region Nested types
    /// <summary>Container for nested types declared in the MultiSig message type.</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static partial class Types {
      public sealed partial class SignatureByValidator : pb::IMessage<SignatureByValidator> {
        private static readonly pb::MessageParser<SignatureByValidator> _parser = new pb::MessageParser<SignatureByValidator>(() => new SignatureByValidator());
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public static pb::MessageParser<SignatureByValidator> Parser { get { return _parser; } }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public static pbr::MessageDescriptor Descriptor {
          get { return global::Phorkus.Proto.MultiSig.Descriptor.NestedTypes[0]; }
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        pbr::MessageDescriptor pb::IMessage.Descriptor {
          get { return Descriptor; }
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public SignatureByValidator() {
          OnConstruction();
        }

        partial void OnConstruction();

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public SignatureByValidator(SignatureByValidator other) : this() {
          Key = other.key_ != null ? other.Key.Clone() : null;
          Value = other.value_ != null ? other.Value.Clone() : null;
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public SignatureByValidator Clone() {
          return new SignatureByValidator(this);
        }

        /// <summary>Field number for the "key" field.</summary>
        public const int KeyFieldNumber = 1;
        private global::Phorkus.Proto.PublicKey key_;
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public global::Phorkus.Proto.PublicKey Key {
          get { return key_; }
          set {
            key_ = value;
          }
        }

        /// <summary>Field number for the "value" field.</summary>
        public const int ValueFieldNumber = 2;
        private global::Phorkus.Proto.Signature value_;
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public global::Phorkus.Proto.Signature Value {
          get { return value_; }
          set {
            value_ = value;
          }
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public override bool Equals(object other) {
          return Equals(other as SignatureByValidator);
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public bool Equals(SignatureByValidator other) {
          if (ReferenceEquals(other, null)) {
            return false;
          }
          if (ReferenceEquals(other, this)) {
            return true;
          }
          if (!object.Equals(Key, other.Key)) return false;
          if (!object.Equals(Value, other.Value)) return false;
          return true;
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public override int GetHashCode() {
          int hash = 1;
          if (key_ != null) hash ^= Key.GetHashCode();
          if (value_ != null) hash ^= Value.GetHashCode();
          return hash;
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public override string ToString() {
          return pb::JsonFormatter.ToDiagnosticString(this);
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public void WriteTo(pb::CodedOutputStream output) {
          if (key_ != null) {
            output.WriteRawTag(10);
            output.WriteMessage(Key);
          }
          if (value_ != null) {
            output.WriteRawTag(18);
            output.WriteMessage(Value);
          }
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public int CalculateSize() {
          int size = 0;
          if (key_ != null) {
            size += 1 + pb::CodedOutputStream.ComputeMessageSize(Key);
          }
          if (value_ != null) {
            size += 1 + pb::CodedOutputStream.ComputeMessageSize(Value);
          }
          return size;
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public void MergeFrom(SignatureByValidator other) {
          if (other == null) {
            return;
          }
          if (other.key_ != null) {
            if (key_ == null) {
              key_ = new global::Phorkus.Proto.PublicKey();
            }
            Key.MergeFrom(other.Key);
          }
          if (other.value_ != null) {
            if (value_ == null) {
              value_ = new global::Phorkus.Proto.Signature();
            }
            Value.MergeFrom(other.Value);
          }
        }

        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        public void MergeFrom(pb::CodedInputStream input) {
          uint tag;
          while ((tag = input.ReadTag()) != 0) {
            switch(tag) {
              default:
                input.SkipLastField();
                break;
              case 10: {
                if (key_ == null) {
                  key_ = new global::Phorkus.Proto.PublicKey();
                }
                input.ReadMessage(key_);
                break;
              }
              case 18: {
                if (value_ == null) {
                  value_ = new global::Phorkus.Proto.Signature();
                }
                input.ReadMessage(value_);
                break;
              }
            }
          }
        }

      }

    }
    #endregion

  }

  #endregion

}

#endregion Designer generated code
