#! /bin/bash
DIR=$(dirname $0)

dotnet tool uninstall --global VeriSol
dotnet tool uninstall --global SolToBoogieTest
dotnet build --configuration Release $DIR/Sources/VeriSol.sln
dotnet tool install VeriSol --version 0.1.1-alpha --global --add-source $DIR/Sources/nupkg/
dotnet tool install --global SolToBoogieTest --version 0.1.1-alpha --add-source $DIR/Sources/nupkg/
