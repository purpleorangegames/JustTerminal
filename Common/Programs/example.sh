# example: copies an example to ~/examples, to read, change and run
# example alone lists them, example raid.cron copies that one

if args() = 0
  for e in /examples
    out bright {pad(name(e), 16)}
    print dim {after(line(e, 1), "# ")}
  end
  stop
end

# the name with or without .sh or .cron
file = arg(1)
if not exists("/examples/" + file) and exists("/examples/" + file + ".sh")
  file = file + ".sh"
else if not exists("/examples/" + file) and exists("/examples/" + file + ".cron")
  file = file + ".cron"
end
if not exists("/examples/" + file)
  print bad no example called {arg(1)}
  stop
end

if exists("~/examples/" + file)
  print bad ~/examples/{file} is there already, rm it first for a new copy
  stop
end
if not exists(~/examples)
  run mkdir ~/examples
end
write ~/examples/{file} = path("/examples/" + file)
print good ~/examples/{file}
if after(file, ".") = "cron"
  print dim cron {before(file, ".")} on turns it on, nano ~/examples/{file} to change it
else
  print dim examples/{file} runs it, nano ~/examples/{file} to change it
end
