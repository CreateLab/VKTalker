# VKTalker
how to run
1) build project
```
$dotnet restore
$dotnet build
```

2) add to bin (executable root) directory file with name Config.json
3) fill config next data

```json
{
  "AppId": 00000000,
  "Login": "login",
  "Password": "password"
}
```

4) run 

```
$dotnet run
```
