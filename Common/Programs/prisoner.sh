# prisoner: the prisoners, in edit mode it sets what the wardens do with one
# prisoner, prisoner 1, prisoner 1 recruit, prisoner Hask release

if args() = 0
  if count(/prisoners) = 0
    print dim no prisoners
    stop
  end
  for p in /prisoners
    out bright {pad(name(p), 4)}
    out {pad(p/name, 16)}
    out dim {pad(p/mode, 34)}
    print resistance {p/resistance}, will {p/will}
  end
  stop
end

found = 0
if exists("/prisoners/" + arg(1))
  p = path("/prisoners/" + arg(1))
  found = 1
end
for x in /prisoners
  if found = 0 and lower(x/name) = lower(replace(arg(1), "_", " "))
    p = x
    found = 1
  end
end
if found = 0
  print bad no prisoner {arg(1)}, prisoner lists them
  stop
end

if args() = 1
  print bright {p/name}: {p/mode}
  print dim can be {p/modes}
  stop
end
set p/mode = arg(2)
print good {p/name}: {p/mode}
