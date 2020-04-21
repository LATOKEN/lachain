# Universal Reed-Solomon Codec
A .NET implementation of the Reed-Solomon algorithm, supporting error, erasure and errata correction.

Uses code from the Reed-Solomon component of the [ZXing.Net project](https://github.com/micjahn/ZXing.Net/tree/master/Source/lib/common/reedsolomon) and code I've ported to C#, from the python code at [Wikiversity](https://en.wikiversity.org/wiki/Reed%E2%80%93Solomon_codes_for_coders).

# Code examples

Create a representation of a Galois field:

```C#
GenericGF field = new GenericGF(285, 256, 0);
```

## Reed-Solomon encoding:

Create an instance of the `ReedSolomonEncoder` class, specifying the Galois field to use:

```C#
ReedSolomonEncoder rse = new ReedSolomonEncoder(field);
```
To encode the string `"Hello World"` with 9 ecc symbols, 9 null-values must be appended to store the ecc symbols:

```C#
int[] data = new int[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
```

Call the `Encode()` method of the `ReedSolomonEncoder` class to encode data with Reed-Solomon:

```C#
rse.Encode(data, 9);
```

The `data` variable now contains:

```
0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x40, 0x86, 0x08, 0xD5, 0x2C, 0xAE, 0xB5, 0x8F, 0x83
```

## Reed-Solomon decoding:

Previous `data` variable with some errors:

```C#
data = new int[] { 0x00, 0x02, 0x02, 0x02, 0x02, 0x02, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x40, 0x86, 0x08, 0xD5, 0x2C, 0xAE, 0xB5, 0x8F, 0x83 };
```

Providing the locations of some erasures:

```C# 
int[] erasures = new int[] { 0, 1, 2 };
```

Create an instance of the `ReedSolomonDecoder` class, specifying the Galois field to use:

```C#
ReedSolomonDecoder rsd = new ReedSolomonDecoder(field);
```

Call the `Decode()` method of the `ReedSolomonDecoder` class to decode (correct) data with Reed-Solomon:

```C#
if (rsd.Decode(data, 9, erasures))
{
    // Data corrected.
}
else
{
    // Too many errors/erasures to correct.
}

```

The `data` variable now contains:

```
0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x40, 0x86, 0x08, 0xD5, 0x2C, 0xAE, 0xB5, 0x8F, 0x83
```

# License

This project uses source files which are under Apache License 2.0, thus this repository is also under this license.
