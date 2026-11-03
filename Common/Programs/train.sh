# train: what an animal learns, an edit mode command
# train Buster, train Buster guard yes, train 2 rescue no

if args() = 0
  print dim usage: train <animal> [what] [yes|no]
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
  print bright {a/name}
  for t in a/training
    out {pad(name(t), 12)}
    print dim {t}
  end
  stop
end
t = "training/" + lower(arg(2))
if not exists(a/{t})
  print bad {a/name} can't learn {arg(2)}
  stop
end
on = "yes"
if args() > 2
  on = arg(3)
end
set a/{t} = on
print good {a/name} {arg(2)}: {a/{t}}
