# group: make a group of colonists, an edit mode command
# group miners Drill Wolf Kovul, then work @miners mining 1
# group alone lists them, group miners shows who is in it

if args() = 0
  if count(/groups) = 0
    print dim no groups yet, make one with group <name> <pawns...>
  end
  for g in /groups
    out bright {pad("@" + name(g), 14)}
    print {g}
  end
  stop
end

g = after("@" + arg(1), "@")
if args() = 1
  if exists("/groups/" + g)
    print {path("/groups/" + g)}
  else
    print bad no group called {g}
  end
  stop
end

# try, so a name that isn't there shows as a warning
try set /groups/{g} = rest(2)
if error = none
  print good @{g}: {path("/groups/" + g)}
end
