# the stock that's running low, change the numbers to what you want to keep

out {pad("food", 12)}
if /stock/food < 5
  print bad {/stock/food} days
else
  print good {/stock/food} days
end

out {pad("medicine", 12)}
if /stock/medicine < 10
  print bad {/stock/medicine}
else
  print good {/stock/medicine}
end

out {pad("steel", 12)}
if /stock/steel < 200
  print bad {/stock/steel}
else
  print good {/stock/steel}
end

out {pad("components", 12)}
if /stock/components < 10
  print bad {/stock/components}
else
  print good {/stock/components}
end
