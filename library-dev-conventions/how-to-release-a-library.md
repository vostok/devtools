## How to release a Vostok library

This is a step-by-step guide describing the process of releasing a new version of Vostok library.

1. Select version number for the new release according to [SemVer](https://semver.org/spec/v1.0.0.html) conventions. Remember to bump major version when making changes that break backward compatibility.

2. Ensure that `VersionPrefix` tag in library's .csproj contains selected version number.

3. Document changes about to be released in `CHANGELOG.md` file.

4. After performing the steps above, push to repository's `master` branch.

5. While staying on `master` branch, create and push a tag with name `release/{version-number}`, where `{version-number}` corresponds to the value selected in step (1).
Example: 
```
git tag release/1.2.4
git push origin release/1.2.4
```

6. Pushing the tag should have started a special build on Appveyor. Let it finish and ensure there were no failures. Check that a NuGet package with proper version and dependencies was published to [nuget.org](https://nuget.org).

7. Increment the version number in library .csproj and push the changes to `master`. This is needed in order to ensure that future prerelease versions are newer than the release you've just published. Cancel the Appveyor build triggered by this change.

<br/>

### What if something goes wrong with Appveyor build?

If the failure is transient or not caused by the library itself, you can restart the build by deleting and creating the release tag from scratch:

```
git tag -d release/1.2.4
git push origin :release/1.2.4
git tag release/1.2.4 abcdef12
git push origin release/1.2.4
```

Note that it's only feasible when a NuGet package of that version hasn't been published yet.
