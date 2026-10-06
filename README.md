# GTE — Get To Evac

Cooperative multiplayer zombie survival game built with Unity and Netcode for GameObjects.

[Experience, trailer and project details](https://www.omidameri.com/project-gte.html) · [Omid Ameri's portfolio](https://www.omidameri.com/)

## The experience

Players gather supplies in a safe room. Leaving the room starts escalating zombie attacks and a hidden helicopter arrival timer. Players move toward extraction and survive until the helicopter is available. Arriving early means defending the landing area; arriving later permits immediate entry into the extraction phase. The surviving player can continue if a teammate dies. Health and temporary weapon power-ups support survival. Designed for up to eight players; development testing primarily used a host and one client.

Team: Omid Ameri, Julong Yan and Kareem Salem. The complete project contains additional controls, timing and networking documentation.

## Explore the implementation

This public repository is a source-code showcase of a collaborative university project. It contains selected project scripts, their Unity metadata, the package manifest and the original editor version. It does not contain the complete playable project.

Project code is organized under:

- `Assets/Scripts/`
- `Assets/Input/`
- `Assets/Editor/`
- `Assets/UI/`


| Area | Entry point |
| --- | --- |
| Safe-room encounter trigger | [SafeRoomExitTrigger.cs](Assets/Scripts/SafeRoomExitTrigger.cs) |
| Zombie spawning | [ZombieSpawner.cs](Assets/Scripts/ZombieSpawner.cs) |
| Enemy state machine | [AI.cs](Assets/Scripts/StateMachine/AI.cs) |
| Player control | [MultiplayerPlayerController.cs](Assets/Scripts/MultiplayerPlayerController.cs) |
| Helicopter arrival and extraction | [ExtractionManager.cs](Assets/Scripts/ExtractionManager.cs) |
| Health and survival | [Health.cs](Assets/Scripts/Health.cs) |

## Dependencies and running the project

The original project uses Unity **6000.2.7f2**. Review `Packages/manifest.json` inside the project folder for its package dependencies. To use these scripts, create an appropriate Unity project and restore the required packages. Scenes, prefabs, input bindings, art, audio and third-party plugins must be obtained and configured separately; cloning this showcase alone will not reproduce the game or experience.

Third-party Asset Store packages, vendor SDK source, course starter code, tutorial examples, models, textures, audio, compiled builds and generated editor files are intentionally excluded. Any plugins referenced by the scripts must be installed from their original publishers under the applicable licenses.

Course-provided networking and lobby scaffolding is excluded from this showcase. The gameplay scripts reference those services; equivalent Netcode/Unity Gaming Services plumbing must be supplied to run them.

## Authorship and provenance

Developed collaboratively by the project team, including Omid Ameri. The code is presented as team work, not as an assertion that every file was written by one person. The complete project, contributor history, branches and tags are preserved in a separate private archive. This public showcase begins with a fresh snapshot so excluded files cannot be recovered from older commits.

No blanket open-source license is granted by this publication. Existing authorship and rights remain applicable; obtain permission from the relevant authors before reusing code.
