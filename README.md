# acdp-ace-extractors

This repository houses AC Data Platform (ACDP) utilities which use
[ACEmulator](https://github.com/ACEmulator/ACE) components to build
datasets used for enrichment. These are convenience tools that eliminate
what would be manual conversion or reimplementation of logic in other code.
They generate SQLite database files used in other parts of ACDP for enrichment.
By automating these processes and not baking in assumptions about, say, the
end of retail functionality, ACDP should remain flexible enough to handle
servers with customization.

## ACDP?

ACDP is a streaming data pipeline that aims to offer near real-time
information about what is happening on an ACEmulator server.

## Extractors

There are currently two extractors:

**ace-enum-extractor**
: This extracts all of the enumeration labels and values from the
  `ACE.Entity.Enum` namespace along with some of their extensions.
  It uses the .NET Reflection APIs to inspect the compiled
  `ACE.Entity.dll` artifact from ACEmulator.

**ace-portal-dat-extractor**
: This uses the code in the `ACE.DatLoader` namespace to read
  `client_portal.dat` and save some of the tables therein. Currently saved
  are the skills table, the XP steps for attributes, trained and specialized
  skills, and vitals, the list of contracts, and the formulae for secondary
  attributes.

## Notes

Currently, the relevant components of ACEmulator are vended into the project
(in the `vendor` directory). To be clear, I did not contribute to the
development of this code from ACEMulator. This project mirrors its
license. Current commit hash: `89f6f207baa34b5a5f7edbeab61ced31630451bf`.