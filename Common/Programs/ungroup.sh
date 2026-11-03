# ungroup: remove a group, an edit mode command
# ungroup miners

if args() = 0
  print dim usage: ungroup <name>
  stop
end
g = after("@" + arg(1), "@")
if not exists("/groups/" + g)
  print no group called {g}
  stop
end
set /groups/{g} = none
print removed @{g}
