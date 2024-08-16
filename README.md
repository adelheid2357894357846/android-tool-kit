# Android Toolkit

**Android Toolkit** is a .NET-based application designed to manage and interact with Android devices using ADB commands.

## Features

- **Start and Stop ADB Server**
- **Check Connected Devices**
- **Execute Shell Commands**
- **Load Device Information**
- **Check Bootloader Status**
- **Advanced Device Checks**

## Getting Started

### Prerequisites

- **.NET Framework**: Ensure you have the .NET Framework installed on your system. The application is compatible with .NET Framework 4.8 or later.
- **ADB**: Android Debug Bridge (ADB) is included in the toolkit. Ensure that it is properly set up on your system.

### Building the Project

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/adelheid2357894357846/android-tool-kit

    Open the Solution:
    Navigate to the project directory and open AndroidToolkit.sln in Visual Studio.

    Build the Project:
    Go to Build > Build Solution in Visual Studio or use the following command in the terminal:

    bash

    msbuild /p:Configuration=Release

Running the Application

    From Visual Studio: Press F5 to run the application with debugging or Ctrl+F5 to run without debugging.
    From the Output Directory: Navigate to the output directory (typically bin\Release) and execute AndroidToolkit.exe.

Usage

    Info Command: Type info in the command box to get details about the toolkit’s features and usage instructions.
    Shell Command: Type shell to enter ADB shell mode and execute shell commands on the connected Android device.

Contributing

    Fork the Repository: Create your own fork of the repository on GitHub.
    Clone Your Fork: Clone the fork to your local machine.
    Create a Branch: Create a new branch for your changes.
    Make Changes: Implement your changes or add features.
    Push Changes: Push your changes to your forked repository.
    Submit a Pull Request: Open a pull request to merge your changes into the main repository.

License

This project is licensed under the MIT License. See the LICENSE file for details.
Contact

For issues or feature requests, please open an issue on GitHub Issues.
Acknowledgements

    GitHub CLI
    Visual Studio
    Android Debug Bridge (ADB)