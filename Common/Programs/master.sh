# master: who handles an animal, an edit mode command
# master Buster, master Buster Tasca, master 2 none

if args() = 0
  for a in /animals
    out bright {pad(name(a), 4)}
    out {pad(a/name, 16)}
    print dim {a/master}
  end
  stop
end
found = 0
if exists("/animals/" + arg(1))
  a = path("/animals/" + arg(1))
  found = 1
end
for x in /animals
  if found = 0 and lower(x/name) = lower(replace(arg(1), "_", " "))
    a = x
    found = 1
  end
end
if found = 0
  print bad no animal {arg(1)}
  stop
end
if args() = 1
  print {a/name}: {a/master}
  stop
end
set a/master = arg(2)
print good {a/name}: {a/master}
