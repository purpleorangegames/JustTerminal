# storage: stockpiles and shelves, save what one takes and put it on others
# storage                      lists them
# storage save 3 meals         keeps what 3 takes in ~/storage/meals
# storage apply meals shelves  in edit mode, on every shelf
# apply takes a number, zones, shelves, all, or a room like kitchen
# linked shelves are one entry, they share what they take

if args() = 0
  for s in /storage
    out bright {pad(name(s), 4)}
    out {pad(s/name, 26)}
    if s/linked > 0
      out dim {pad("linked " + s/linked, 10)}
    else
      out dim {pad(s/kind, 10)}
    end
    out dim {pad(s/room, 16)}
    print {s/priority}
  end
  stop
end

if arg(1) = "save"
  n = arg(2)
  if args() < 3 or not exists("/storage/" + n)
    print bad usage: storage save <number> <name>
    stop
  end
  if not exists(~/storage)
    run mkdir ~/storage
  end
  file = arg(3)
  write ~/storage/{file} = path("/storage/" + n + "/filter")
  print good ~/storage/{file}
  stop
end

if arg(1) = "apply"
  if args() < 3
    print bad usage: storage apply <name> <number|zones|shelves|all|room>
    stop
  end
  if not exists("~/storage/" + arg(2))
    print bad no saved filter called {arg(2)}
    stop
  end
  filter = path("~/storage/" + arg(2))
  which = lower(arg(3))
  n = 0
  for s in /storage
    if which = "all" or which = s/kind + "s" or which = "shelves" and s/kind = "building" or which = s/room or which = name(s)
      set s/filter = filter
      n = n + 1
    end
  end
  print good {n} changed
  stop
end

print bad storage, storage save or storage apply
