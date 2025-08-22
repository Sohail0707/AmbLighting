# AmbLighting Setup Instructions

Follow these steps to set up and run this project on any Windows machine:

## Prerequisites

- **.NET 10.0 SDK** (or newer) must be installed. Download from: https://dotnet.microsoft.com/download
- (Optional) **Git** for version control: https://git-scm.com/downloads

## Setup Steps

1. **Clone the repository:**
   ```sh
   git clone https://github.com/Sohail0707/AmbLighting.git
   cd AmbLighting
   ```
2. **Restore dependencies:**
   ```sh
   dotnet restore ColorExtractor/ColorExtractor.csproj
   ```
3. **Build the project:**
   ```sh
   dotnet build ColorExtractor/ColorExtractor.csproj -c Release
   ```
4. **Run the program:**
   ```sh
   ColorExtractor\bin\Release\net10.0-windows\ColorExtractor.exe
   ```

## Notes

- This project requires Windows due to its use of Windows Forms and screen capture APIs.
- If you encounter errors about missing dependencies, ensure you have the correct .NET SDK and are on Windows 10 or later.
- For any issues, check the README or open an issue on the repository.
