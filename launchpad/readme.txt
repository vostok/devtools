A .NET Core CLI tool used to bootstrap repositories from project templates.

Installation (you'll need .NET Core 2.1+):

dotnet tool install -g Vostok.Launchpad

Update:

dotnet tool update -g Vostok.Launchpad

Launch the tool without parameters to get help.

zsh users need to add following line to ~/.zshrc (see https://github.com/dotnet/cli/issues/9321#issuecomment-390720940):

export PATH="$HOME/.dotnet/tools:$PATH"
