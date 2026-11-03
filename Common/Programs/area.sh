# area: show or set the allowed area, an edit mode command
# area, area Tasca home, area all unrestricted

# area alone lists the areas
if args() = 0
  for a in /areas
    out   {pad(name(a), 24)}
    print dim {a}
  end
  stop
end

if args() = 1
  for p in pawns(arg(1))
    out bright {pad(p/name, 14)}
    if exists(p/area)
      print {p/area}
    else
      print dim -
    end
  end
  stop
end

# try, so one colonist away on a caravan doesn't stop the others
changed = ""
for p in pawns(arg(1))
  try set p/area = arg(2)
  if error = none
    if changed != ""
      changed = changed + ", "
    end
    changed = changed + p/name
  end
end
if changed != ""
  print good {changed}: {lower(arg(2))}
end
