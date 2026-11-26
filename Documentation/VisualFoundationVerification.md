# Milestone 10A verification

The starting point is integrated 9C at `e7b2f7141fb9116628b91e520540b177072901a2` from [PR #26](https://github.com/ChuckreimuHakunorris/castrum-tactics/pull/26). The verified stack uses Godot 4.7.2 .NET and the repository's .NET SDK pin.

[Visual architecture](VisualArchitecture.md) records the ownership and format decisions. The [visual roadmap](VisualDevelopmentRoadmap.md) tracks 10B through 10F. All three reference previews supplied in the review conversation were inspected and recorded in [art direction](ArtDirection.md). The external Windows originals were unavailable. Original format, alpha, provenance and exact extraction remain 10B prerequisites.

## Reproduction

Use the [engineering commands](EngineeringVerification.md#local-commands-and-ci) for build, tests, formatting, archive and documentation checks. The [platform guide](PlatformVerification.md) supplies the two headless presentation commands via the README, plus managed build and export procedures. Compare seeded fixed and natural histories against the integrated baseline using the [9A comparison script](ArchitectureRefactoring.md#reproduction) and a separate output directory for each build and ruleset.
