# bed: the colonists' beds, in edit mode it gives one to someone
# bed, bed Tasca 3, bed none 3 empties bed 3

if args() < 2
  for b in /beds
    out bright {pad(name(b), 4)}
    out {pad(b/name, 22)}
    out dim {pad(b/room, 14)}
    print {b/owner}
  end
  stop
end
if not exists("/beds/" + arg(2))
  print bad no bed {arg(2)}, bed lists them
  stop
end
b = path("/beds/" + arg(2))
set b/owner = arg(1)
print good bed {arg(2)}: {b/owner}
