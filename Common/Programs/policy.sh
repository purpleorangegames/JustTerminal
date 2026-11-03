# policy: show or set apparel, food, drugs or reading policy, an edit mode command
# policy, policy Drill, policy Drill food, policy @miners food lavish

# policy alone lists them all
if args() = 0
  for kind in /policies
    out bright {pad(kind, 10)}
    list = ""
    for pol in kind
      list = list + name(pol) + "  "
    end
    print {list}
  end
  stop
end

# a name shows their four policies
if args() = 1
  out {pad("", 14)}
  for kind in /policies
    out dim {pad(upper(kind), 16)}
  end
  print
  for p in pawns(arg(1))
    out bright {pad(p/name, 14)}
    for kind in /policies
      k = name(kind)
      if exists(p/policy/{k})
        out {pad(p/policy/{k}, 16)}
      else
        out {pad("-", 16)}
      end
    end
    print
  end
  stop
end

k = lower(arg(2))
if not exists("/policies/" + k)
  print bad kind is one of apparel, food, drugs, reading
  stop
end

# a name and a kind shows what they have and what else there is
if args() = 2
  for p in pawns(arg(1))
    out bright {pad(p/name, 14)}
    if exists(p/policy/{k})
      print {p/policy/{k}}
    else
      print -
    end
  end
  others = ""
  for pol in path("/policies/" + k)
    others = others + "  " + name(pol)
  end
  print dim other {k} policies:{others}
  stop
end

changed = ""
for p in pawns(arg(1))
  try set p/policy/{k} = rest(3)
  if error = none
    if changed != ""
      changed = changed + ", "
    end
    changed = changed + p/name
  end
end
if changed != ""
  print good {changed}: {k} {lower(rest(3))}
end
