# bills: the workbenches and their bills
# in edit mode a program can change them: set b/list/01/suspended = yes, set b/list/01/count = 10

if count(/bills) = 0
  print dim no bills
  stop
end
for b in /bills
  print bright {b/name}
  for x in b/list
    out   {pad(x/name, 30)}
    if exists(x/mode)
      if x/mode = "forever"
        out dim {pad("forever", 16)}
      else if x/mode = "until"
        out dim {pad("until " + x/count + ", have " + x/have, 16)}
      else
        out dim {pad(x/count + " more", 16)}
      end
    end
    if x/suspended = yes
      out warn suspended
    end
    print
  end
end
